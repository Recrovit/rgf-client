using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfDynamicDictionary : DynamicObject, IDictionary<string, object>, IEquatable<RgfDynamicDictionary>
{
    public RgfDynamicDictionary() { _data = new(); }
    public RgfDynamicDictionary(RgfDynamicDictionary data) { _data = data._data; }
    public RgfDynamicDictionary(Dictionary<string, object> data) { _data = data; }
    public RgfDynamicDictionary(IEqualityComparer<string> comparer) { _data = new Dictionary<string, object>(comparer); }
    public RgfDynamicDictionary(string[] key, object[] value)
    {
        if (key?.Length != value?.Length)
        {
            throw new ArgumentException("The number of keys and values does not match.");
        }

        _data = new();
        if (key != null && value != null)
        {
            for (int i = 0; i < key.Length; i++)
            {
                _data[key[i]] = value[i];
            }
        }
    }

    private Dictionary<string, object> _data { get; set; }
    private Dictionary<string, RgfDynamicData> _dynData { get; set; } = new();
    private readonly object _lock = new object();

    public bool TryGetMember(string key, out object result) => _data.TryGetValue(key, out result);
    public override bool TryGetMember(GetMemberBinder binder, out object result) => TryGetMember(binder.Name, out result);
    public override bool TrySetMember(SetMemberBinder binder, object value) { SetMember(binder.Name, value); return true; }

    public void SetMember(string key, object value)
    {
        lock (_lock)
        {
            if (_dynData.TryGetValue(key, out var dynValue))
            {
                dynValue.Value = value;
            }
            else
            {
                _data[key] = value;
            }
        }
    }
    public object GetMember(string key)
    {
        lock (_lock)
        {
            if (_dynData.TryGetValue(key, out var dynValue))
            {
                return dynValue.Value;
            }
            object value;
            TryGetMember(key, out value);
            return value;
        }
    }

    public RgfDynamicData GetItemData(string key)
    {
        lock (_lock)
        {
            RgfDynamicData dynValue;
            if (!_dynData.TryGetValue(key, out dynValue))
            {
                object value = null;
                _data.TryGetValue(key, out value);
                dynValue = new RgfDynamicData(key, value);
                _dynData.Add(key, dynValue);
            }
            return dynValue;
        }
    }

    public object this[string key] { get => GetMember(key); set => SetMember(key, value); }

    public static RgfDynamicDictionary Create(ILogger logger, RgfEntity entityDesc, Dictionary<string, object> data) => Create(logger, entityDesc, data.Keys.ToArray(), data.Values.ToArray());
    public static RgfDynamicDictionary Create(ILogger logger, RgfEntity entityDesc, string[] dataColumns, object[] dataArray, bool htmlDecode = false)
    {
        var dynData = new RgfDynamicDictionary();
        RgfProperty prop = null;
        object data = null;
        try
        {
            for (int i = 0; i < dataColumns.Length; i++)
            {
                prop = entityDesc.Properties.SingleOrDefault(e => e.ClientName == dataColumns[i]);
                if (prop == null)
                {
                    prop = entityDesc.Properties.SingleOrDefault(e => e.Alias.Equals(dataColumns[i], StringComparison.OrdinalIgnoreCase));
                }
                string name = prop?.Alias ?? dataColumns[i];
                data = dataArray[i];

                object value = data;
                if (data != null && prop != null)
                {
                    value = RgfDynamicData.ConvertToTypedValue(prop.ClientDataType, data);
                    if (value != null && htmlDecode && prop.ListType == PropertyListType.String)
                    {
                        value = System.Web.HttpUtility.HtmlDecode(value.ToString());
                    }
                }
                dynData.SetMember(name, value);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RgfDynamicData.Create => name:{prop}, data:{data}, ClientDataType:{ClientDataType}, Culture:{CultureInfo}", prop?.Alias, data, prop?.ClientDataType, CultureInfo.CurrentCulture.Name);
        }
        return dynData;
    }

    //The GetDynamicMemberNames method of DynamicObject class must be overridden and return the property names to perform data operation and editing while using DynamicObject.
    public override IEnumerable<string> GetDynamicMemberNames() => _data.Keys;

    #region IDictionary
    public ICollection<string> Keys => ((IDictionary<string, object>)_data).Keys;
    public ICollection<object> Values => ((IDictionary<string, object>)_data).Values;
    public int Count => ((ICollection<KeyValuePair<string, object>>)_data).Count;
    public bool IsReadOnly => ((ICollection<KeyValuePair<string, object>>)_data).IsReadOnly;
    public void Add(string key, object value) => ((IDictionary<string, object>)_data).Add(key, value);
    public bool ContainsKey(string key) => ((IDictionary<string, object>)_data).ContainsKey(key);
    public bool Remove(string key) => ((IDictionary<string, object>)_data).Remove(key);
    public bool TryGetValue(string key, out object value) => ((IDictionary<string, object>)_data).TryGetValue(key, out value);
    public void Add(KeyValuePair<string, object> item) => ((ICollection<KeyValuePair<string, object>>)_data).Add(item);
    public void Clear() => ((ICollection<KeyValuePair<string, object>>)_data).Clear();
    public bool Contains(KeyValuePair<string, object> item) => ((ICollection<KeyValuePair<string, object>>)_data).Contains(item);
    public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => ((ICollection<KeyValuePair<string, object>>)_data).CopyTo(array, arrayIndex);
    public bool Remove(KeyValuePair<string, object> item) => ((ICollection<KeyValuePair<string, object>>)_data).Remove(item);
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, object>>)_data).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_data).GetEnumerator();
    #endregion

    public bool Equals(RgfDynamicDictionary other)
    {
        if (other == null && this.Count != other.Count)
        {
            return false;
        }
        foreach (var key in this.Keys)
        {
            if (!other.ContainsKey(key))
            {
                return false;
            }

            var data1 = this.GetItemData(key);
            var data2 = other.GetItemData(key);
            if (!data1.Equals(data2))
            {
                return false;
            }
        }
        return true;
    }
}
