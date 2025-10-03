// Assets/Obscurus/Scripts/Core/Save/SaveCodec.cs
#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System.Text;
using UnityEngine;
#if HAS_ODIN
using Sirenix.Serialization;
#endif

namespace Obscurus.Save
{
    public static class SaveCodec
    {
#if HAS_ODIN
        public static string ToJson<T>(T value, bool pretty = true)
        {
            var bytes = SerializationUtility.SerializeValue(value, DataFormat.JSON);
            return Encoding.UTF8.GetString(bytes);
        }
        public static T FromJson<T>(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            return SerializationUtility.DeserializeValue<T>(bytes, DataFormat.JSON);
        }
#else
        // >>> FIX: Unity JsonUtility neumí primitiva → obalíme do Box<T>
        [System.Serializable] private class Box<T> { public T v; }

        public static string ToJson<T>(T value, bool pretty = true)
        {
            var box = new Box<T> { v = value };
            return JsonUtility.ToJson(box, pretty);
        }
        public static T FromJson<T>(string json)
        {
            var box = JsonUtility.FromJson<Box<T>>(json);
            return box != null ? box.v : default;
        }
#endif
    }
}