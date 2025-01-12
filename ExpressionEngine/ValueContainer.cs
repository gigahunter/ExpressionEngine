﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExpressionEngine
{
    /// <summary>
    /// Generic container that can contain the values used in expressions, the creation of these from C# types and
    /// getting them out of the container.
    /// </summary>
    [JsonConverter(typeof(ValueContainerConverter))]
    public class ValueContainer : IComparable, IEquatable<ValueContainer>
    {
        private readonly object _value;
        private readonly ValueType _type;

        public object Value { get => _value; }

        /// <summary>
        /// Creates Value Container from string
        /// </summary>
        /// <param name="value">value which should be contained</param>
        /// <param name="tryToParse">Try to parse the string int, decimal or boolean</param>
        public ValueContainer(string value, bool tryToParse = false)
        {
            if (value == null)
            {
                _value = null;
                _type = ValueType.Null;
                return;
            }

            if (tryToParse)
            {
                if (value.Contains('.') && decimal.TryParse(value, out var fValue))
                {
                    _value = fValue;
                    _type = ValueType.Float;
                }
                else if (int.TryParse(value, out var iValue))
                {
                    _value = iValue;
                    _type = ValueType.Integer;
                }
                else if (bool.TryParse(value, out var bValue))
                {
                    _value = bValue;
                    _type = ValueType.Boolean;
                }
                else
                {
                    _value = value;
                    _type = ValueType.String;
                }
            }
            else
            {
                _value = value;
                _type = ValueType.String;
            }
        }

        public ValueContainer(string stringValue)
        {
            _value = stringValue;
            _type = ValueType.String;
        }

        public ValueContainer(Guid guid)
        {
            _value = guid;
            _type = ValueType.Guid;
        }

        public ValueContainer(DateTimeOffset dateTime)
        {
            _value = dateTime;
            _type = ValueType.Date;
        }

        public ValueContainer(float floatValue)
        {
            _value = Convert.ToDecimal(floatValue);
            _type = ValueType.Float;
        }

        public ValueContainer(decimal floatValue)
        {
            _value = floatValue;
            _type = ValueType.Float;
        }

        public ValueContainer(double floatValue)
        {
            _value = Convert.ToDecimal(floatValue);
            _type = ValueType.Float;
        }

        public ValueContainer(int intValue)
        {
            _value = intValue;
            _type = ValueType.Integer;
        }

        public ValueContainer(bool boolValue)
        {
            _value = boolValue;
            _type = ValueType.Boolean;
        }

        public ValueContainer(List<ValueContainer> arrayValue)
        {
            _value = arrayValue;
            _type = ValueType.Array;
        }

        public ValueContainer(IEnumerable<ValueContainer> arrayValue)
        {
            _value = arrayValue.ToList();
            _type = ValueType.Array;
        }

        internal ValueContainer(Dictionary<string, ValueContainer> objectValue, bool normalize)
        {
            _value = normalize ? objectValue.Normalize() : objectValue;

            _type = ValueType.Object;
        }

        public ValueContainer(Dictionary<string, ValueContainer> objectValue)
        {
            _value = objectValue.Normalize();
            _type = ValueType.Object;
        }

        public ValueContainer(ValueContainer valueContainer)
        {
            _type = valueContainer._type;
            _value = valueContainer._value;
        }

        public ValueContainer(object obj)
        {
            _value = obj;
            _type = ValueType.Object;
        }

        public ValueContainer()
        {
            _type = ValueType.Null;
            _value = null;
        }

        public ValueType Type()
        {
            return _type;
        }

        public bool IsNumber()
        {
            return _type == ValueType.Float || _type == ValueType.Integer;
        }

        /// <summary>
        /// Get the value of the ValueContainer has it's respective C# type
        ///
        /// <code>double</code> and <code>float</code> is converted to <code>decimal</code>.
        /// </summary>
        /// <typeparam name="T">Target ype</typeparam>
        /// <returns>Value of ValueContainer</returns>
        /// <exception cref="ExpressionEngineException">When T is not castable to ValueContainer type.</exception>
        public T GetValue<T>()
        {
            if (_value.GetType() == typeof(T))
            {
                return (T)_value;
            }

            if (IsNumber() && (typeof(T) == typeof(decimal) || typeof(T) == typeof(double) ||
                               typeof(T) == typeof(float) || typeof(T) == typeof(int)))
            {
                return (T)Convert.ChangeType(_value, typeof(T));
            }

            if (_type == ValueType.Array)
            {
                switch (typeof(T))
                {
                    case { } iEnumerable when iEnumerable == typeof(IEnumerable<ValueContainer>):
                        return (T)GetValue<List<ValueContainer>>().AsEnumerable();
                        // case { } array when array == typeof(ValueContainer[]):
                        // return GetValue<List<ValueContainer>>().ToArray();
                }
            }

            throw new ExpressionEngineException(
                $"Cannot convert ValueContainer of type {_value.GetType()} to {typeof(T)}");
        }

        public ValueContainer this[int i]
        {
            get
            {
                if (_type != ValueType.Array)
                {
                    throw new InvalidOperationException("Index operations can only be performed on arrays.");
                }

                return AsList()[i];
            }
            set
            {
                if (_type != ValueType.Array)
                {
                    throw new InvalidOperationException("Index operations can only be performed on arrays.");
                }

                ((List<ValueContainer>)_value)[i] = value;
            }
        }

        public ValueContainer this[string key]
        {
            get
            {
                if (_type != ValueType.Object)
                {
                    throw new InvalidOperationException("Index operations can only be performed on objects.");
                }

                var keyPath = key.Split('/');

                var current = GetValue<Dictionary<string, ValueContainer>>()[keyPath.First()];
                foreach (var xKey in keyPath.Skip(1))
                {
                    current = current.GetValue<Dictionary<string, ValueContainer>>()[xKey]; // Does not
                }

                return current;
            }
            set
            {
                if (_type != ValueType.Object)
                {
                    throw new InvalidOperationException("Index operations can only be performed on objects.");
                }

                var keyPath = key.Split('/');
                var finalKey = keyPath.Last();

                var current = this;
                foreach (var xKey in keyPath.Take(keyPath.Length - 1))
                {
                    var dict = GetValue<Dictionary<string, ValueContainer>>();
                    var success = dict.TryGetValue(xKey, out var temp);

                    if (success)
                    {
                        current = temp;
                    }
                    else
                    {
                        dict[xKey] = new ValueContainer(new Dictionary<string, ValueContainer>());
                        current = dict[xKey];
                    }
                }

                current.AsDict()[finalKey] = value;
            }
        }

        public Dictionary<string, ValueContainer> AsDict()
        {
            if (_type == ValueType.Object)
            {
                return GetValue<Dictionary<string, ValueContainer>>();
            }

            throw new ExpressionEngineException("Can't get none object value container as dict.");
        }

        public List<ValueContainer> AsList()
        {
            if (_type == ValueType.Array)
            {
                return GetValue<List<ValueContainer>>();
            }

            throw new ExpressionEngineException("Can't get none object value container as array.");
        }

        /// <inheritdoc />
        public override string ToString()
        {
            /*
             * This is called when debugging to display text in the variable overview.
             * This is also used when parsing.
             */
            return _type switch
            {
                ValueType.Boolean => _value.ToString(),
                ValueType.Integer => _value.ToString(),
                ValueType.Float => _value.ToString(),
                ValueType.String => _value.ToString(),
                ValueType.Object => "{" + string.Join(",", GetValue<Dictionary<string, ValueContainer>>()
                    .Select(kv => kv.Key + "=" + kv.Value).ToArray()) + "}",
                ValueType.Array => "[" + string.Join(", ", GetValue<List<ValueContainer>>().ToList()) + "]",
                ValueType.Null => "<null>",
                ValueType.Date => _value.ToString(),
                ValueType.Guid => _value.ToString(),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public bool IsNull()
        {
            return _type == ValueType.Null;
        }

        public int CompareTo(object obj)
        {
            if (obj == null || obj.GetType() != GetType())
                throw new InvalidOperationException("Cannot compare these two...");

            var other = (ValueContainer)obj;

            switch (_value)
            {
                case bool b:
                    return b.CompareTo(other._value);

                case int i when other._type == ValueType.Integer:
                    return i.CompareTo(other._value);

                case int _ when other.Type() == ValueType.Float:
                    return GetValue<decimal>().CompareTo(other._value);

                case float _:
                case double _:
                    throw new ExpressionEngineException("float or double not possible");
                case decimal f when other._type == ValueType.Float:
                    return f.CompareTo(other._value);

                case decimal f when other._type == ValueType.Integer:
                    return f.CompareTo(other.GetValue<decimal>());

                case string s:
                    return s.CompareTo(other._value);

                case Guid g:
                    return g.CompareTo(other._value);

                case Dictionary<string, ValueContainer> d:
                    var d2 = (Dictionary<string, ValueContainer>)other._value;
                    return d.Count - d2.Count;

                case List<ValueContainer> l:
                    var l2 = (List<ValueContainer>)other._value;
                    return l.Count() - l2.Count();

                default:
                    return -1;
            }
        }

        public bool Equals(ValueContainer other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            switch (_type)
            {
                case ValueType.Array when other._type == ValueType.Array:
                    {
                        var thisArray = (List<ValueContainer>)_value;
                        var otherArray = other.GetValue<List<ValueContainer>>();

                        return thisArray.SequenceEqual(otherArray);
                    }
                case ValueType.Object when other._type == ValueType.Object:
                    {
                        var thisDict = AsDict();
                        var otherDict = other.AsDict();

                        return thisDict.Count == otherDict.Count && !thisDict.Except(otherDict).Any();
                    }
                case ValueType.Integer:
                case ValueType.Float:
                    {
                        return 0 == CompareTo(other);
                    }
                default:
                    return Equals(_value, other._value) && _type == other._type;
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((ValueContainer)obj);
        }

        public override int GetHashCode()
        {
            return new { _type, _value }.GetHashCode();
        }

        public bool ContainsKey(string key)
        {
            if (_type != ValueType.Object) return false;

            var current = AsDict();
            var keys = key.Split('/');
            for (var i = 0; i < keys.Length - 1; i++)
            {
                if (current.ContainsKey(keys[i]))
                {
                    current = current[keys[i]].AsDict();
                }
                else
                {
                    return false;
                }
            }

            return current.ContainsKey(keys[keys.Length - 1]);
        }
    }

    public static class ValueContainerExtension
    {
        public static async ValueTask<ValueContainer> CreateValueContainerFromJToken(JToken json,
            IExpressionEngine expressionEngine = null)
        {
            if (json == null)
            {
                return new ValueContainer();
            }

            var v = await JsonToValueContainer(json, expressionEngine);
            return v;
        }

        private static async ValueTask<ValueContainer> JsonToValueContainer(JToken json,
            IExpressionEngine expressionEngine)
        {
            switch (json)
            {
                case JObject _:
                    {
                        var dictionary = json.OfType<JProperty>().ToDictionary(pair => pair.Name,
                            token => { return JsonToValueContainer(token.Value, expressionEngine); });

                        var pairs = await Task.WhenAll
                        (
                            dictionary.Select
                            (
                                async pair => new { pair.Key, Value = await pair.Value }
                            )
                        );

                        return new ValueContainer(pairs.ToDictionary(pair => pair.Key, pair => pair.Value));
                    }
                case JArray jArray:
                    return jArray.Count == 0
                        ? new ValueContainer()
                        : await JArrayToValueContainer(jArray, expressionEngine);

                case JValue jValue:
                    if (jValue.HasValues)
                    {
                        throw new ExpressionEngineException(
                            "When parsing JToken to ValueContainer, the JToken as JValue can only contain one value.");
                    }

                    return jValue.Type switch
                    {
                        JTokenType.Boolean => new ValueContainer(jValue.Value<bool>()),
                        JTokenType.Integer => new ValueContainer(jValue.Value<int>()),
                        JTokenType.Float => new ValueContainer(jValue.Value<float>()),
                        JTokenType.Null => new ValueContainer(),
                        JTokenType.String when expressionEngine != null => await expressionEngine.ParseToValueContainer(
                            jValue.Value<string>()),
                        JTokenType.String => new ValueContainer(jValue.Value<string>()),
                        JTokenType.None => new ValueContainer(),
                        JTokenType.Guid => new ValueContainer(jValue.Value<Guid>()),
                        JTokenType.Date => new ValueContainer(jValue.Value<DateTimeOffset>()),
                        _ => throw new ExpressionEngineException(
                            $"{jValue.Type} is not yet supported in ValueContainer conversion")
                    };

                default:
                    throw new ExpressionEngineException("Could not parse JToken to ValueContainer. " + json.Type);
            }
        }

        private static async ValueTask<ValueContainer> JArrayToValueContainer(JArray json,
            IExpressionEngine expressionEngine)
        {
            var list = new List<ValueContainer>();

            foreach (var jToken in json)
            {
                list.Add(await JsonToValueContainer(jToken, expressionEngine));
            }

            return new ValueContainer(list);
        }
    }

    public class ValueContainerComparer : EqualityComparer<ValueContainer>
    {
        public override bool Equals(ValueContainer x, ValueContainer y)
        {
            if (x == null || y == null)
            {
                return x == null && y == null;
            }

            return x.Equals(y);
        }

        public override int GetHashCode(ValueContainer obj)
        {
            return obj.GetHashCode();
        }
    }
}