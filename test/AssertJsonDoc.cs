using System.Reflection;
using System.Text.Json;

namespace OSSMWebServer.Test
{
    public static class AssertJsonDoc
    {
        public static void Satisfies(object expected, JsonElement actual)
        {
            if (actual.ValueKind != JsonValueKind.Object)
                throw new Exception($"Expected JSON object, got {actual.ValueKind}");

            PropertyInfo[] props = expected.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

            foreach (PropertyInfo prop in props)
            {
                if (!actual.TryGetProperty(prop.Name, out var jsonValue))
                    throw new Exception($"Missing property '{prop.Name}'");
                ValidateValue(prop.Name, jsonValue, prop.GetValue(expected));
            }
        }

        private static void ValidateValue(string name, JsonElement json, object? expected)
        {
            if (expected is null)
            {
                if (json.ValueKind != JsonValueKind.Null)
                    throw new Exception($"Property '{name}' expected null");
                return;
            }

            Type expectedType = expected.GetType();

            // Delegate (Func<T, bool>)
            if (typeof(Delegate).IsAssignableFrom(expectedType))
            {
                MethodInfo method = expectedType.GetMethod("Invoke")!;
                Type paramType = method.GetParameters()[0].ParameterType;

                object? actualValue = ConvertJson(json, paramType);

                if (!(bool)method.Invoke(expected, [actualValue])!)
                    throw new Exception($"Predicate failed for '{name}' (value: {actualValue})");

                return;
            }

            // Type check only
            if (expected is Type targetType)
            {
                try { _ = ConvertJson(json, targetType); }
                catch (Exception)
                {
                    throw new Exception($"Property '{name}' type mismatch. Expected {targetType.Name}, got {json.ValueKind}");
                }
                return;
            }

            // Nested object
            if (!IsSimple(expectedType))
            {
                if (json.ValueKind != JsonValueKind.Object)
                    throw new Exception($"Property '{name}' expected object");

                Satisfies(expected, json);
                return;
            }

            // Simple value comparison
            var converted = ConvertJson(json, expectedType);

            if (!Equals(converted, expected))
                throw new Exception($"Property '{name}' mismatch. Expected {expected}, got {converted}");
        }

        private static object? ConvertJson(JsonElement json, Type targetType)
        {
            try
            {
                if (targetType == typeof(string)) return json.GetString();
                if (targetType == typeof(int)) return json.GetInt32();
                if (targetType == typeof(long)) return json.GetInt64();
                if (targetType == typeof(bool)) return json.GetBoolean();
                if (targetType == typeof(double)) return json.GetDouble();
                if (targetType == typeof(decimal)) return json.GetDecimal();

                // Fallback
                return JsonSerializer.Deserialize(json.GetRawText(), targetType);
            }
            catch (Exception ex)
            {
                throw new Exception($"Type conversion failed to {targetType.Name}: {ex.Message}");
            }
        }

        private static bool IsSimple(Type type)
        {
            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal);
        }
    }
}
