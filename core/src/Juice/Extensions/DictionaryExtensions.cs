using System.Collections;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Juice.Extensions
{
    public static partial class DictionaryExtensions
    {

        public const string DefaultReplacementPattern = "\\$\\((?<name>[\\$\\w\\[\\]'\"\\.]+)\\)";

        private static string EscapeStringValue(string value)
        {
            return value;
        }

        /// <summary>
        /// Get the value of the key as a string that is replaced the variables from the input options with
        /// <see cref="replacementParamPattern"/>
        /// <example>For example:
        /// <code>
        ///     var dict = new Dictionary&lt;string, object&gt;{ { "basePath", "http://localhost"}, { "str", "$(basePath)/abc/xyz"} };<br/>
        ///     console.WriteLine(dict.GetOptionAsString("str"));<br/><br/>
        ///     //http://localhost/abc/xyz
        /// </code>
        ///</example>
        /// </summary>
        /// <param name="options"></param>
        /// <param name="key"></param>
        /// <param name="replacementParamPattern"></param>
        /// <returns>formatted string</returns>
        public static string GetOptionAsString(this IDictionary<string, object> options, string key, string replacementParamPattern = DefaultReplacementPattern)
        {
            return options.GetOptionAsString(key, options, replacementParamPattern);
        }

        /// <summary>
        /// Get the value of the key as a string that is replaced the variables from the input options with
        /// <see cref="replacementParamPattern"/>
        ///<para>You can logging replacement process with Action&lt;string&gt; logging</para>
        /// <example>For example:
        /// <code>
        ///     var dict = new Dictionary&lt;string, object&gt;{ { "basePath", "http://localhost"}, { "str", "$(basePath)/abc/xyz"} };<br/>
        ///     console.WriteLine(dict.GetOptionAsString("str", DictionaryExtensions.DefaultReplacementPattern, s => Console.WriteLine(s)));<br/><br/>
        ///     //Replace input, origin value= basePath, new value= http://localhost<br/>
        ///     //http://localhost/abc/xyz
        /// </code>
        ///</example>
        /// </summary>
        /// <param name="options"></param>
        /// <param name="key"></param>
        /// <param name="replacementParamPattern"></param>
        /// <param name="logging"></param>
        /// <returns>formatted string</returns>
        public static string GetOptionAsString(this IDictionary<string, object> options, string key, string replacementParamPattern, Action<string> logging)
        {
            return options.GetOptionAsString(key, options, replacementParamPattern, logging);
        }

        /// <summary>
        /// Get the value of the key as a string that is replaced the variables from the input options with
        /// <see cref="DefaultReplacementPattern"/>
        ///<para>You can logging replacement process with Action&lt;string&gt; logging</para>
        /// <example>For example:
        /// <code>
        ///     var dict = new Dictionary&lt;string, object&gt;{ { "basePath", "http://localhost"}, { "str", "$(basePath)/abc/xyz"} };<br/>
        ///     console.WriteLine(dict.GetOptionAsString("str", s => Console.WriteLine(s)));<br/><br/>
        ///     //Replace input, origin value= basePath, new value= http://localhost <br/>
        ///     //http://localhost/abc/xyz 
        /// </code>
        ///</example>
        /// </summary>
        /// <param name="options"></param>
        /// <param name="key"></param>
        /// <param name="logging"></param>
        /// <returns>formatted string</returns>
        public static string GetOptionAsString(this IDictionary<string, object> options, string key, Action<string> logging)
        {
            return options.GetOptionAsString(key, options, DefaultReplacementPattern, logging);
        }

        /// <summary>
        /// Get the value of the key as a string that is replaced the variables from the input options or <see cref="referencedOptions"/> with
        /// <see cref="replacementParamPattern"/>
        ///<para>You can logging replacement process with Action&lt;string&gt; logging</para>
        /// <para>NOTE: This function alway find variables value in self dictionary before try to find in referenced dictionary</para>
        /// <example>For example:
        /// <code>
        ///     var dict = new Dictionary&lt;string, object&gt;{ { "str", "$(basePath)/abc/xyz"} };<br/>
        ///     var refDict = new Dictionary&lt;string, object&gt;{{ "basePath", "http://localhost"}};<br/>
        /// 
        ///     console.WriteLine(dict.GetOptionAsString("str", refDict, <see cref="DefaultReplacementPattern"></see>, s => Console.WriteLine(s)));<br/><br/>
        ///     //Replace input, origin value= basePath, new value= http://localhost<br/>
        ///     //http://localhost/abc/xyz
        /// </code>
        ///</example>
        /// </summary>
        /// <param name="oc"></param>
        /// <param name="key"></param>
        /// <param name="referencedOptions"></param>
        /// <param name="replacementParamPattern"></param>
        /// <param name="logging"></param>
        /// <returns></returns>
        public static string GetOptionAsString(this IDictionary<string, object> oc, string key, IDictionary<string, object> referencedOptions, string replacementParamPattern = DefaultReplacementPattern, Action<string> logging = null)
        {
            var value = GetOption<string>(oc, key, null, referencedOptions);
            if (value != null && !string.IsNullOrEmpty(replacementParamPattern))
            {
                var input = value.ToString();
                MatchCollection matches = Regex.Matches(input, replacementParamPattern);
                foreach (Match m in matches)
                {
                    var paramName = m.Groups["name"].ToString();
                    var paramValue = oc.GetOptionAsString(paramName, referencedOptions, replacementParamPattern, logging);
                    if (paramValue != null)
                    {
                        input = input.Replace(m.Value, EscapeStringValue(paramValue));
                        logging?.Invoke($"Replace input, origin value= {m.Value}, new value= {EscapeStringValue(paramValue)}");
                    }
                    else
                    {
                        paramValue = referencedOptions.GetOptionAsString(paramName, replacementParamPattern, logging);
                        if (paramValue != null)
                        {
                            input = input.Replace(m.Value, EscapeStringValue(paramValue));
                            logging?.Invoke($"Replace input, origin value= {m.Value}, new value= {EscapeStringValue(paramValue)}");
                        }
                    }
                }

                return input;
            }
            return value;
        }

        /// <summary>
        /// <see cref="GetOptionAsString(IDictionary{string, object}, string, IDictionary{string, object}, string, Action{string})"/> with <see cref="DefaultReplacementPattern"/><br/>
        /// Get the value of the key as a string that is replaced the variables from the input options or <see cref="referencedOptions"/> with <see cref="DefaultReplacementPattern"/>
        /// <para>NOTE: This function alway find variables value in self dictionary before try to find in referenced dictionary</para>
        /// <example>For example:
        /// <code>
        ///     var dict = new Dictionary&lt;string, object&gt;{ { "str", "$(basePath)/abc/xyz"} };<br/>
        ///     var refDict = new Dictionary&lt;string, object&gt;{{ "basePath", "http://localhost"}};<br/>
        /// 
        ///     console.WriteLine(dict.GetOptionAsString("str", refDict, s => Console.WriteLine(s)));<br/><br/>
        ///     //Replace input, origin value= basePath, new value= http://localhost<br/>
        ///     //http://localhost/abc/xyz
        /// </code>
        ///</example>
        /// </summary>
        /// <param name="oc"></param>
        /// <param name="key"></param>
        /// <param name="referencedOptions"></param>
        /// <param name="logging"></param>
        /// <returns></returns>
        public static string GetOptionAsString(this IDictionary<string, object> oc, string key, IDictionary<string, object> referencedOptions, Action<string> logging = null)
            => oc.GetOptionAsString(key, referencedOptions, DefaultReplacementPattern, logging);

        /// <summary>
        /// Get the value of the key as <see cref="T"/> type
        /// <para>If the value is starts with $ but not $( then we will try to find it as key in self-dict or reference dict</para>
        /// <para>Support access object field or array element with . and [] pattern</para>
        /// <example><code>
        /// var dict = new Dictionary&lt;string, object&gt;{<br/>
        ///     { "childDict", new Dictionary&lt;string, object&gt;{<br/>
        ///         {"key1", "val1"},<br/>
        ///         {"key2", new string[]{ "e0", "e1" }}
        ///     }}<br/>
        /// };<br/>
        /// console.WriteLine(dict.GetOption&lt;string&gt;("childDict.key1"));<br/>
        /// console.WriteLine(dict.GetOption&lt;string&gt;("childDict.key2[1]"));<br/><br/>
        /// // val1<br/>
        /// // e1
        /// </code></example>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="options"></param>
        /// <param name="key"></param>
        /// <param name="converter"></param>
        /// <param name="referencedOptions"></param>
        /// <returns></returns>
        public static T GetOption<T>(this IDictionary<string, object> options, string key, Func<object, T> converter = null, IDictionary<string, object> referencedOptions = null)
        {
            var value = options.ContainsKey(key) ? options[key] : null;
            if (value != null)
            {
                if (value.ToString().StartsWith("$") && !value.ToString().StartsWith("$("))
                {
                    return options.Match(value.ToString(), referencedOptions, converter);
                }
                if (typeof(T) == typeof(string) && value != null && value.ToString().StartsWith("\\$"))
                {
                    value = value.ToString().TrimStart('\\');
                }
                if (converter == null)
                {
                    return Converter<T>(value);
                }
                return converter(value);
            }
            return options.Match(key, referencedOptions, converter);
        }

        private static T Match<T>(this IDictionary<string, object> options, string key, IDictionary<string, object> referencedOptions, Func<object, T> converter = null)
        {
            if (key.StartsWith("$") || key.Contains(".") || key.Contains("["))
            {
                object value = null;
                var isFirstKey = true;
                foreach (Match m in Regex.Matches(key, @"([$\w\/\\]+)"))
                {
                    if (isFirstKey)
                    {
                        isFirstKey = false;
                        var firstKey = m.Value;
                        value = options.ContainsKey(firstKey) ? options[firstKey] : null;
                        if (value == null)
                        {
                            if (firstKey.ToString().StartsWith("$") && referencedOptions != null)
                            {
                                var val1 = GetOption<object>(referencedOptions, firstKey.ToString());
                                if (val1 != null)
                                {
                                    value = val1;
                                }
                            }
                            else
                            {
                                return default(T);
                            }
                        }
                        else if (value.ToString().StartsWith("$") && referencedOptions != null)
                        {
                            var val1 = GetOption<object>(referencedOptions, value.ToString());
                            if (val1 != null)
                            {
                                value = val1;
                            }
                        }
                    }
                    else
                    {
                        value = GetItemValue(value, m.Value);
                        if (value == null)
                        {
                            return default(T);
                        }
                        if (value is string && value.ToString().StartsWith("$") && referencedOptions != null)
                        {
                            var val1 = GetOption<object>(referencedOptions, value.ToString());
                            if (val1 != null)
                            {
                                value = val1;
                            }
                        }
                    }

                }

                if (converter == null)
                {
                    return Converter<T>(value);
                }
                return converter(value);

            }
            return default(T);
        }

        private static T Converter<T>(object value)
        {
            if (value == null) { return default(T); }
            if (value is T t) { return t; }

            if (typeof(T) == typeof(string))
            {
                object obj = value.ToString();
                return (T)(obj);
            }
            try
            {
                if (typeof(T) == typeof(Guid))
                {
                    if (string.IsNullOrWhiteSpace(value.ToString())) { return (T)(object)Guid.Empty; }
                    object guid = Guid.Parse(value.ToString());
                    return (T)guid;
                }

                if (typeof(T).IsEnum)
                {
                    var e = Enum.Parse(typeof(T), value.ToString());
                    return (T)e;
                }
            }
            catch
            {
            }
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch { }
            return JsonConverter<T>(value);
        }

        private static T JsonConverter<T>(object value)
        {
            if (value == null) { return default(T); }
            var serialized = JsonConvert.SerializeObject(value);
            if (string.IsNullOrWhiteSpace(serialized)) { return default(T); }
            try
            {
                return JsonConvert.DeserializeObject<T>(serialized);
            }
            catch (Exception)
            {
                return default(T);
            }
        }

        private static object GetItemValue(object myObject, object index)
        {
            if (myObject == null)
            {
                return null;
            }
            else
            {

                // not sure which index(es) you want
                Type t = myObject.GetType();
                if (typeof(IList).IsAssignableFrom(t))
                {
                    try
                    {
                        int indexInt = int.Parse(index.ToString());
                        IList ilist = (IList)myObject;
                        return ilist[indexInt];
                    }
                    catch (Exception)
                    {

                    }
                }

                var indexer = t.GetProperties()
                    .Where(p => p.GetIndexParameters().Length != 0)
                    .FirstOrDefault();
                try
                {
                    if (indexer != null)
                    {
                        object[] indexArgs = { index };
                        return indexer.GetValue(myObject, indexArgs);
                    }
                }
                catch (Exception) { }
                try
                {
                    var dyn = myObject as dynamic;
                    if (dyn != null)
                    {
                        return dyn[index];
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        var p = myObject.GetType().GetProperty(index.ToString());
                        if (p != null)
                        {
                            return p.GetValue(myObject);
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Merge hierarchy key-value from other dictionaries into current dict if it does not contains key or matched rules:
        /// <para><c>ignoreNull: true</c> and <c>force: false</c> => only merge if value from other dict is not null and value in self-dict is null</para>
        /// <para><c>ignoreNull: true</c> and <c>force: true</c> => merge if value from other dict is not null, even when value in self-dict is null or not</para>
        /// <para><c>ignoreNull: false</c> and <c>force: false</c> => merge if value from other dict is null or not and value in self-dict is null or equals key</para>
        /// <para><c>ignoreNull: false</c> and <c>force: true</c> => merge if value from other dict is null or not, even when value in self-dict is null or not</para>
        /// </summary>
        /// <param name="options"></param>
        /// <param name="ignoreNull"></param>
        /// <param name="force"></param>
        /// <param name="others"></param>
        /// <returns></returns>
        public static IDictionary<string, object?> MergeOptions(this IDictionary<string, object?> options, bool ignoreNull, bool force, params IDictionary<string, object?>[] others)
        {
            foreach (var other in others)
            {
                if (other == null) { continue; }

                foreach (var kv in other)
                {
                    if (options.ContainsKey(kv.Key))
                    {
                        var val1 = options[kv.Key];
                        var val2 = other[kv.Key];
                        if (val1 != null && val2 != null && typeof(IDictionary<string, object?>).IsAssignableFrom(val1.GetType()) && typeof(IDictionary<string, object?>).IsAssignableFrom(val2.GetType()))
                        {
                            options[kv.Key] = ((IDictionary<string, object?>)options[kv.Key])?.MergeOptions(ignoreNull, (IDictionary<string, object?>)val2);
                        }
                        else if ((!ignoreNull || val2 != null) && (force || val1 == null || val1.ToString() == kv.Key))
                        {
                            options[kv.Key] = val2;
                        }
                    }
                    else
                    {
                        options.Add(kv.Key, kv.Value);
                    }
                }
            }
            return options;
        }

        /// <summary>
        /// Merge hierarchy key-value from other dictionaries into current dict if it does not contains key or matched rules:
        /// <para><c>ignoreNull: true</c> => only merge if value from other dict is not null and value in self-dict is null</para>
        /// <para><c>ignoreNull: false</c> => merge if value from other dict is null or not and value in self-dict is null or equals key</para>
        /// </summary>
        /// <param name="options"></param>
        /// <param name="ignoreNull"></param>
        /// <param name="others"></param>
        /// <returns></returns>
        public static IDictionary<string, object?> MergeOptions(this IDictionary<string, object?> options, bool ignoreNull, params IDictionary<string, object?>[] others)
            => MergeOptions(options, ignoreNull, false, others);

        /// <summary>
        /// Merge hierarchy key-value from other dictionaries into current dict when:
        /// <para>current dict does not contains key</para>
        /// <para>OR: value in self-dict is null or equals key and key exists in other dict</para>
        /// </summary>
        /// <param name="options"></param>
        /// <param name="others"></param>
        /// <returns></returns>
        public static IDictionary<string, object?> MergeOptions(this IDictionary<string, object?> options, params IDictionary<string, object?>[] others)
        {
            return options.MergeOptions(false, others);
        }

        /// <summary>
        /// If key contains ".", it will be translate to hierarchy object
        /// <example><para>For example:</para>
        /// <code>
        /// var dict = new Dictionary&lt;string,object&gt;().Set("User.Name", "Abc");<br/>
        /// console.WriteLine(JsonConvert.SerializeObject(dict));<br/>
        /// // {"User": {"Name": "Abc"}}
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="options"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static IDictionary<string, object> Set(this IDictionary<string, object> options, string key, object value)
        {
            string[] vs = key.Split('.');

            for (var i = vs.Length - 1; i >= 0; i--)
            {
                if (i == 0)
                {
                    options[vs[i]] = value;
                }
                else
                {
                    value = new Dictionary<string, object>().Set(vs[i], value);
                }
            }
            return options;
        }

        /// <summary>
        /// options["yyyy"] = date.Year.ToString("D4");
        /// <para>options["MM"] = date.Month.ToString("D2");</para>
        /// <para>options["dd"] = date.Day.ToString("D2");</para>
        /// <para>options["dddd"] = date.ToString("dddd");</para>
        /// <para> options["MMMM"] = date.ToString("MMMM");</para>
        /// </summary>
        /// <param name="options"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        public static IDictionary<string, object> ParseDate(this IDictionary<string, object> options, DateTimeOffset date)
        {
            options["yyyy"] = date.Year.ToString("D4");
            options["MM"] = date.Month.ToString("D2");
            options["dd"] = date.Day.ToString("D2");
            options["dddd"] = date.ToString("dddd");
            options["MMMM"] = date.ToString("MMMM");
            return options;
        }
    }
}
