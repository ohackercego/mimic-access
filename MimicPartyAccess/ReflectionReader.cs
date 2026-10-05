using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MimicPartyAccess;

/// <summary>
/// Reads values from generated IL2CPP proxy objects without compile-time game references.
/// </summary>
internal static class ReflectionReader
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<string, Type> TypeCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<(Type Target, Type Component), MethodInfo> ComponentQueries = new();
    private static readonly Dictionary<(Type Target, Type Component), Func<object, object?>> ComponentLookups = new();

    public static Type? FindTypeByName(string fullName)
    {
        if (TypeCache.TryGetValue(fullName, out var cached))
            return cached;
        var candidates = fullName.StartsWith("Il2Cpp", StringComparison.Ordinal)
            ? new[] { fullName }
            : new[] { fullName, "Il2Cpp" + fullName };

        foreach (var candidate in candidates)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(candidate, throwOnError: false, ignoreCase: false);
                    if (type != null)
                    {
                        TypeCache[fullName] = type;
                        return type;
                    }
                }
                catch
                {
                    // Some generated Unity proxy assemblies contain unrelated types that cannot be reflected.
                }
            }
        }

        return null;
    }

    public static object? ReadMember(object? target, string name)
    {
        if (target == null)
            return null;

        var type = target.GetType();
        var property = type.GetProperty(name, InstanceFlags);
        if (property != null && property.GetIndexParameters().Length == 0)
            return property.GetValue(target);

        for (var current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, InstanceFlags | BindingFlags.DeclaredOnly);
            if (field != null)
                return field.GetValue(target);
        }

        return null;
    }

    public static string? ReadText(object? textComponent)
    {
        var text = ReadMember(textComponent, "text") as string;
        return string.IsNullOrWhiteSpace(text) ? null : CleanText(text);
    }

    public static IEnumerable<object> GetComponentsInChildren(object target, Type componentType)
    {
        var key = (target.GetType(), componentType);
        if (ComponentQueries.TryGetValue(key, out var cached))
            return Enumerate(cached.Invoke(target, new object[] { false }));
        var method = target.GetType().GetMethods(InstanceFlags)
            .FirstOrDefault(candidate => candidate.Name == "GetComponentsInChildren" &&
                candidate.IsGenericMethodDefinition && candidate.GetGenericArguments().Length == 1 &&
                candidate.GetParameters().Length == 1 && candidate.GetParameters()[0].ParameterType == typeof(bool));
        if (method == null)
            throw new MissingMethodException(target.GetType().FullName, "GetComponentsInChildren<T>(bool)");

        var query = method.MakeGenericMethod(componentType);
        ComponentQueries[key] = query;
        return Enumerate(query.Invoke(target, new object[] { false }));
    }

    public static object? GetComponent(object target, Type componentType)
    {
        if (target == null)
            return null;

        var key = (target.GetType(), componentType);
        if (ComponentLookups.TryGetValue(key, out var cached))
            return cached(target);

        var byType = target.GetType().GetMethods(InstanceFlags)
            .FirstOrDefault(candidate => candidate.Name == "GetComponent" && !candidate.IsGenericMethodDefinition &&
                candidate.GetParameters().Length == 1 && candidate.GetParameters()[0].ParameterType == typeof(Type));
        var generic = byType == null
            ? target.GetType().GetMethods(InstanceFlags)
                .FirstOrDefault(candidate => candidate.Name == "GetComponent" && candidate.IsGenericMethodDefinition &&
                    candidate.GetGenericArguments().Length == 1 && candidate.GetParameters().Length == 0)
            : null;

        Func<object, object?> lookup;
        if (byType != null)
            lookup = instance => byType.Invoke(instance, new object[] { componentType });
        else if (generic != null)
        {
            var query = generic.MakeGenericMethod(componentType);
            lookup = instance => query.Invoke(instance, null);
        }
        else
            throw new MissingMethodException(target.GetType().FullName, "GetComponent");

        ComponentLookups[key] = lookup;
        return lookup(target);
    }

    public static string CleanText(string value)
    {
        var withoutMarkup = Regex.Replace(value, "<[^>]+>", " ");
        withoutMarkup = Regex.Replace(withoutMarkup, "[\u200B-\u200F\uFEFF]", string.Empty);
        return Regex.Replace(withoutMarkup, @"\s+", " ").Trim();
    }

    public static IEnumerable<object> Enumerate(object? collection)
    {
        if (collection == null)
            yield break;

        if (collection is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item != null)
                    yield return item;
            }

            yield break;
        }

        var getEnumerator = collection.GetType().GetMethod("GetEnumerator", InstanceFlags, null, Type.EmptyTypes, null);
        var enumerator = getEnumerator?.Invoke(collection, null);
        if (enumerator == null)
            yield break;

        var moveNext = enumerator.GetType().GetMethod("MoveNext", InstanceFlags, null, Type.EmptyTypes, null);
        var current = enumerator.GetType().GetProperty("Current", InstanceFlags);
        if (moveNext == null || current == null)
            yield break;

        while (moveNext.Invoke(enumerator, null) is bool hasNext && hasNext)
        {
            var item = current.GetValue(enumerator);
            if (item != null)
                yield return item;
        }
    }
}
