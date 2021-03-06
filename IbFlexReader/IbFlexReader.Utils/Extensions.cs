﻿namespace IbFlexReader.Utils
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using IbFlexReader.Contracts;
    using IbFlexReader.Contracts.Attributes;
    using IbFlexReader.Contracts.Ib;

    public static class Extensions
    {
        private static Type referenceType = typeof(TradeConfirm);

        public static TIn PopulateFrom<TIn, TFrom>(this TIn obj, TFrom from, List<ErrorMessage> errorObjects) where TIn : class
        {
            if (from == null)
            {
                return obj;
            }

            var typeFrom = from.GetType();
            var typeTo = obj.GetType();
            var typeToProperties = typeTo.GetProperties();
            var errorFound = false;
            
            foreach (var p in typeFrom.GetProperties())
            {
                try
                {
                    // just for debugging purposes
                    var pName = p.Name;
                    var possible = typeToProperties.FirstOrDefault(x => x.Name == p.Name);

                    if (possible != null)
                    {
                        var possibleType = possible.PropertyType;

                        if (possibleType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(possibleType))
                        {
                            var listType = typeof(List<>).MakeGenericType(possibleType.GenericTypeArguments[0]);
                            var list = (IList)Activator.CreateInstance(listType);
                            possible.SetValue(obj, list);

                            foreach (var o in (IEnumerable)p.GetValue(from))
                            {
                                var entryType = referenceType.Assembly.GetType(referenceType.Namespace + "." + o.GetType().Name);
                                var newInstance = Activator.CreateInstance(entryType).PopulateFrom(o, errorObjects);
                                if (newInstance != null)
                                {
                                    list.Add(newInstance);
                                }
                            }
                        }
                        else if (possibleType.FullName.Contains("IbFlexReader") && !possibleType.IsEnum && (!Nullable.GetUnderlyingType(possibleType)?.IsEnum ?? true))
                        {
                            var instance = Activator.CreateInstance(possibleType);

                            possible.SetValue(obj, instance.PopulateFrom(p.GetValue(from), errorObjects));
                        }
                        else
                        {
                            possible.SetValue(obj, CastValue(from, p.GetValue(from), possible));
                        }
                    }
                } 
                catch (Exception e)
                {
                    var msg = $"error during casting field '{p.Name}' of '{typeFrom.Name}' with message: {e.Message.ToString()} and stacktrace: {e.StackTrace.ToString()}";
                    errorObjects.Add(new ErrorMessage
                    {
                        Message = msg,
                        Object = GetJson(from, typeFrom)
                    });
                    errorFound = true;
                    break;
                }
            }

            return errorFound ? null : obj;
        }

        private static string GetJson(object obj, Type type)
        {
            var jsonSerializer = new DataContractJsonSerializer(type);
            var memStream = new MemoryStream();
            jsonSerializer.WriteObject(memStream, obj);
            memStream.Seek(0, SeekOrigin.Begin);
            return Encoding.UTF8.GetString(memStream.GetBuffer());
        }

        private static object GetValueOfProperty(object obj, string name)
        {
            var type = obj.GetType();
            var prop = type.GetProperty(name);
            return prop.GetValue(obj);
        }

        private static object CastValue(object valueHolder, object value, PropertyInfo property)
        {
            if (value == null)
            {
                return value;
            }

            var strVal = value.ToString();

            if (strVal == string.Empty && property.PropertyType != typeof(string))
            {
                return null;
            }

            var type = property.PropertyType;

            if (Nullable.GetUnderlyingType(type)?.IsEnum ?? false)
            {
                return EnumParser.EnumParser.Parse(Nullable.GetUnderlyingType(type), strVal);
            }

            if (type == typeof(DateTime?))
            {
                // expect format
                var formatAttributes = property.GetCustomAttributes<FormatAttribute>();

                if (!formatAttributes.Any())
                {
                    throw new Exception("format not specified");
                }

                try
                {
                    return DateTime.ParseExact(strVal, formatAttributes.FirstOrDefault(x => x.Order == 0).Value, CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                    return DateTime.ParseExact(strVal, formatAttributes.FirstOrDefault(x => x.Order != 0).Value, CultureInfo.InvariantCulture);
                }                
            }

            if (type == typeof(int?))
            {
                return (int?)int.Parse(strVal);
            }

            if (type == typeof(long?))
            {
                return (long?)long.Parse(strVal);
            }

            if (type == typeof(double?))
            {
                return double.Parse(strVal, CultureInfo.InvariantCulture);
            }
            
            return value.ToString();
        }
    }
}
