using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
#if NETSTANDARD1_1
using Microsoft.Extensions.Primitives;
#else
using ServiceStack.Text.Support;
#endif

namespace ServiceStack.Text.Common
{
    public class JsReader<TSerializer>
        where TSerializer : ITypeSerializer
    {
        private static readonly ITypeSerializer Serializer = JsWriter.GetTypeSerializer<TSerializer>();

        public ParseStringDelegate GetParseFn<T>()
        {
            var onDeserializedFn = JsConfig<T>.OnDeserializedFn;
            if (onDeserializedFn != null)
            {
                var parseFn = GetCoreParseFn<T>();
                return value => onDeserializedFn((T)parseFn(value));
            }

            return GetCoreParseFn<T>();
        }

        public ParseStringSegmentDelegate GetParseStringSegmentFn<T>()
        {
            var onDeserializedFn = JsConfig<T>.OnDeserializedFn;
            if (onDeserializedFn != null)
            {
                var parseFn = GetCoreParseStringSegmentFn<T>();
                return value => onDeserializedFn((T)parseFn(value));
            }

            return GetCoreParseStringSegmentFn<T>();
        }

        private ParseStringDelegate GetCoreParseFn<T>()
        {
            return v => GetCoreParseStringSegmentFn<T>()(new StringSegment(v));
        }

        private ParseStringSegmentDelegate GetCoreParseStringSegmentFn<T>()
        {
            var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            if (JsConfig<T>.HasDeserializeFn)
                return value => JsConfig<T>.ParseFn(Serializer, value.Value);

            if (type.IsEnum())
                return x => ParseUtils.TryParseEnum(type, Serializer.UnescapeSafeString(x));

            if (type == typeof(string))
                return Serializer.UnescapeString;

            if (type == typeof(object))
                return DeserializeType<TSerializer>.ObjectStringToType;

            var specialParseFn = ParseUtils.GetSpecialParseMethod(type);
            if (specialParseFn != null)
                return v => specialParseFn(v.Value);

            if (type.IsArray)
            {
                return DeserializeArray<T, TSerializer>.ParseStringSegment;
            }

            var builtInMethod = DeserializeBuiltin<T>.Parse;
            if (builtInMethod != null)
                return value => builtInMethod(Serializer.UnescapeSafeString(value));

            if (type.HasGenericType())
            {
                if (type.IsOrHasGenericInterfaceTypeOf(typeof(IList<>)))
                    return DeserializeList<T, TSerializer>.ParseStringSegment;

                if (type.IsOrHasGenericInterfaceTypeOf(typeof(IDictionary<,>)))
                    return DeserializeDictionary<TSerializer>.GetParseStringSegmentMethod(type);

                if (type.IsOrHasGenericInterfaceTypeOf(typeof(ICollection<>)))
                    return DeserializeCollection<TSerializer>.GetParseStringSegmentMethod(type);

                if (type.HasAnyTypeDefinitionsOf(typeof(Queue<>))
                    || type.HasAnyTypeDefinitionsOf(typeof(Stack<>)))
                    return DeserializeSpecializedCollections<T, TSerializer>.ParseStringSegment;

                if (type.IsOrHasGenericInterfaceTypeOf(typeof(KeyValuePair<,>)))
                    return DeserializeKeyValuePair<TSerializer>.GetParseStringSegmentMethod(type);

                if (type.IsOrHasGenericInterfaceTypeOf(typeof(IEnumerable<>)))
                    return DeserializeEnumerable<T, TSerializer>.ParseStringSegment;

                var customFn = DeserializeCustomGenericType<TSerializer>.GetParseStringSegmentMethod(type);
                if (customFn != null)
                    return customFn;
            }

            var pclParseFn = PclExport.Instance.GetJsReaderParseStringSegmentMethod<TSerializer>(typeof(T));
            if (pclParseFn != null)
                return pclParseFn;

            var isDictionary = typeof(T) != typeof(IEnumerable) && typeof(T) != typeof(ICollection)
                && (typeof(T).AssignableFrom(typeof(IDictionary)) || typeof(T).HasInterface(typeof(IDictionary)));
            if (isDictionary)
            {
                return DeserializeDictionary<TSerializer>.GetParseStringSegmentMethod(type);
            }

            var isEnumerable = typeof(T).AssignableFrom(typeof(IEnumerable))
                || typeof(T).HasInterface(typeof(IEnumerable));
            if (isEnumerable)
            {
                var parseFn = DeserializeSpecializedCollections<T, TSerializer>.ParseStringSegment;
                if (parseFn != null) return parseFn;
            }

            if (type.IsValueType())
            {
                var staticParseMethod = StaticParseMethod<T>.Parse;
                if (staticParseMethod != null)
                    return value => staticParseMethod(Serializer.UnescapeSafeString(value));
            }
            else
            {
                var staticParseMethod = StaticParseRefTypeMethod<TSerializer, T>.Parse;
                if (staticParseMethod != null)
                    return value => staticParseMethod(Serializer.UnescapeSafeString(value));
            }

            var typeConstructor = DeserializeType<TSerializer>.GetParseStringSegmentMethod(TypeConfig<T>.GetState());
            if (typeConstructor != null)
                return typeConstructor;

            var stringConstructor = DeserializeTypeUtils.GetParseStringSegmentMethod(type);
            if (stringConstructor != null) return stringConstructor;

            return DeserializeType<TSerializer>.ParseAbstractType<T>;
        }

    }
}
