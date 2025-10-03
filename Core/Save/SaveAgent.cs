#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

namespace Obscurus.Save
{
    /// <summary>
    /// Univerzální „snapshoter“ – chytá transform, aktivitu, RB, Animator, NavMeshAgent
    /// a serializovatelná pole všech připojených MonoBehaviour (Unity + Odin).
    /// </summary>
    [DisallowMultipleComponent]
#if HAS_ODIN
    [HideMonoScript]
    [InfoBox("Univerzální Save/Load pro objekt. Přidávej přes SaveTag, nebo ručně.", InfoMessageType.Info)]
#endif
    public class SaveAgent : SaveComponent
    {
        public enum Role { Generic, Player }
        public enum SpaceMode { World, Local }

#if HAS_ODIN
        [EnumToggleButtons]
#endif
        public Role role = Role.Generic;

        [Header("Capture")]
        public bool captureTransform = true;
        public SpaceMode transformSpace = SpaceMode.World;
        public bool captureActiveSelf = true;
        public bool captureRigidbody = true;
        public bool captureAnimator = true;
        public bool animatorDeepState = false; // volitelně ulož layer state + normalizedTime
        public bool captureNavMeshAgent = true;
        public bool captureCustomComponents = true;

        [Header("Custom Components")]
        [Tooltip("Uložit i UnityEngine.Object reference? (scénové odkazy se ukládají jako SaveId tak jako tak).")]
        public bool allowUnityObjectRefs = false;

        [Tooltip("Whitelist – pokud vyplníš, ukládají se jen tyto komponenty/jejich pole.")]
        public List<Component> onlyTheseComponents = new();

        [Header("Player extras")]
        [Tooltip("Pitch pivot (X), typicky parent kamery). Jen pro Role=Player.")]
        public Transform cameraPivot;
        public Camera playerCamera;

        // ---------- DATOVÉ SNAPSHOTY ----------

        [Serializable] struct XForm { public Vector3 p, s; public Vector3 e; }
        [Serializable] struct RB { public bool has; public Vector3 vel, angVel; public bool isKinematic, useGravity; }

        [Serializable] struct AnimParam { public string name; public int type; public float f; public int i; public bool b; }
        [Serializable] struct AnimLayer { public int index; public int stateHash; public float normalizedTime; }

        [Serializable] struct NMA
        {
            public bool has;
            public Vector3 dest;
            public bool stopped;
            public float speed, accel, angSpeed;
        }

        [Serializable] struct FieldSnapshot
        {
            public string name;            // field name
            public string type;            // field declared type (AssemblyQualifiedName)
            public bool isRef;             // je to reference na scénový objekt?
            public string refId;           // SaveId cíle (GameObject)
            public string refComponent;    // typ komponenty, pokud field je Component
            public string json;            // inline hodnota (když není ref)
        }

        [Serializable] struct CustomCompSnapshot
        {
            public string type; // komponenta (AssemblyQualifiedName)
            public List<FieldSnapshot> fields;
        }

        [Serializable] class AgentData
        {
            public int version = 1;
            public XForm x;
            public bool activeSelf;

            public RB rb;
            public AnimParam[] anim;
            public AnimLayer[] animLayers;
            public NMA nma;

            public float playerPitch; // když role=Player
            public List<CustomCompSnapshot> custom;
        }

        public override string SaveType => "SaveAgent";

        // ---------- CAPTURE ----------
        public override string CaptureAsJson()
        {
            var data = new AgentData();

            // activeSelf
            if (captureActiveSelf) data.activeSelf = gameObject.activeSelf;

            // transform (∗∗BEZ SIDE-EFFECTŮ∗∗)
            if (captureTransform)
            {
                var t = transform;

                if (role == Role.Player)
                {
                    // Ulož jen pozici + YAW (Y). Pitch ukládáme zvlášť z cameraPivot.
                    data.x.p = t.position;
                    data.x.e = new Vector3(0f, t.eulerAngles.y, 0f);
                    data.x.s = t.lossyScale; // informativní
                }
                else if (transformSpace == SpaceMode.World)
                {
                    data.x.p = t.position;
                    data.x.e = t.rotation.eulerAngles;
                    data.x.s = t.lossyScale;
                }
                else
                {
                    data.x.p = t.localPosition;
                    data.x.e = t.localEulerAngles;
                    data.x.s = t.localScale;
                }
            }

            // Rigidbody
            if (captureRigidbody)
            {
                var rb = GetComponent<Rigidbody>();
                data.rb.has = rb != null;
                if (rb)
                {
#if UNITY_6000_0_OR_NEWER
                    data.rb.vel         = rb.linearVelocity;
#else
                    data.rb.vel         = rb.velocity;
#endif
                    data.rb.angVel      = rb.angularVelocity;
                    data.rb.isKinematic = rb.isKinematic;
                    data.rb.useGravity  = rb.useGravity;
                }
            }

            // Animator
            if (captureAnimator)
            {
                var anim = GetComponent<Animator>();
                if (anim && anim.runtimeAnimatorController)
                {
                    var ps = anim.parameters;
                    var plist = new List<AnimParam>(ps.Length);
                    foreach (var p in ps)
                    {
                        var a = new AnimParam { name = p.name, type = (int)p.type };
                        switch (p.type)
                        {
                            case AnimatorControllerParameterType.Float: a.f = anim.GetFloat(p.name); break;
                            case AnimatorControllerParameterType.Int:   a.i = anim.GetInteger(p.name); break;
                            case AnimatorControllerParameterType.Bool:
                            case AnimatorControllerParameterType.Trigger:
                                a.b = anim.GetBool(p.name); break;
                        }
                        plist.Add(a);
                    }
                    data.anim = plist.ToArray();

                    if (animatorDeepState)
                    {
                        int layers = anim.layerCount;
                        var l = new List<AnimLayer>(layers);
                        for (int i = 0; i < layers; i++)
                        {
                            var st = anim.GetCurrentAnimatorStateInfo(i);
                            l.Add(new AnimLayer { index = i, stateHash = st.fullPathHash, normalizedTime = st.normalizedTime });
                        }
                        data.animLayers = l.ToArray();
                    }
                }
            }

            // NavMeshAgent (bez compile-time závislosti)
            if (captureNavMeshAgent)
            {
                var nma = GetNavMeshAgent();
                data.nma.has = nma != null;
                if (nma)
                {
                    data.nma.dest     = GetProp<Vector3>(nma, "destination");
                    data.nma.stopped  = GetProp<bool>(nma, "isStopped");
                    data.nma.speed    = GetProp<float>(nma, "speed");
                    data.nma.accel    = GetProp<float>(nma, "acceleration");
                    data.nma.angSpeed = GetProp<float>(nma, "angularSpeed");
                }
            }

            // Player pitch (ulož z pivotu/kamery)
            if (role == Role.Player)
            {
                float pitch = 0f;
                if (!cameraPivot && playerCamera) cameraPivot = playerCamera.transform.parent;
                if (cameraPivot) pitch = Normalize180(cameraPivot.localEulerAngles.x);
                data.playerPitch = pitch;
            }

            // Custom komponenty
            if (captureCustomComponents)
                data.custom = CaptureCustomComponents();

            return SaveCodec.ToJson(data, true);
        }

        // ---------- APPLY ----------
        public override void ApplyFromJson(string json)
        {
            var data = SaveCodec.FromJson<AgentData>(json);
            if (data == null) return;

            if (captureActiveSelf) gameObject.SetActive(data.activeSelf);

            // === Hráč: bezpečný warp + SYNC NA PlayerController ===
            if (role == Role.Player)
            {
                if (!cameraPivot && playerCamera) cameraPivot = playerCamera.transform.parent;

                var cc = GetComponent<CharacterController>();
                bool ccWas = cc && cc.enabled;
                if (cc) cc.enabled = false;

                // pozice + YAW (jen osa Y)
                transform.position = data.x.p;
                transform.rotation = Quaternion.Euler(0f, data.x.e.y, 0f);

                // PITCH jen pivot/kamera
                float pitch = Normalize180(data.playerPitch);
                if (cameraPivot)
                    cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                else if (playerCamera)
                    playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

                // --- sync privátních polí PlayerControlleru, ať Update() nic nepřepíše ---
                var pc = GetComponent<PlayerController>();
                if (pc)
                {
                    var tPC = typeof(PlayerController);
                    tPC.GetField("yaw", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(pc, data.x.e.y);
                    tPC.GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(pc, pitch);
                    tPC.GetField("verticalVelocity", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(pc, 0f);
                }

                if (cc) cc.enabled = ccWas;
            }
            else
            {
                // === Generic/NPC/Enemy: běžná aplikace ===
                var cc = GetComponent<CharacterController>();
                bool wasCC = cc && cc.enabled;
                if (cc) cc.enabled = false;

                var rb = GetComponent<Rigidbody>();
                bool wasKin = false; if (rb) { wasKin = rb.isKinematic; rb.isKinematic = true; }

                if (captureTransform)
                {
                    if (transformSpace == SpaceMode.World)
                        transform.SetPositionAndRotation(data.x.p, Quaternion.Euler(data.x.e));
                    else
                    {
                        transform.localPosition = data.x.p;
                        transform.localRotation = Quaternion.Euler(data.x.e);
                        transform.localScale    = data.x.s;
                    }
                }

                if (rb)
                {
                    if (captureRigidbody)
                    {
#if UNITY_6000_0_OR_NEWER
                        rb.linearVelocity  = data.rb.vel;
#else
                        rb.velocity        = data.rb.vel;
#endif
                        rb.angularVelocity = data.rb.angVel;
                        rb.useGravity      = data.rb.useGravity;
                    }
                    rb.isKinematic = wasKin;
                }

                if (cc) cc.enabled = wasCC;
            }

            // Animator apply (společné)
            if (captureAnimator && data.anim != null)
            {
                var anim = GetComponent<Animator>();
                if (anim && anim.runtimeAnimatorController)
                {
                    foreach (var a in data.anim)
                    {
                        switch ((AnimatorControllerParameterType)a.type)
                        {
                            case AnimatorControllerParameterType.Float: anim.SetFloat(a.name, a.f); break;
                            case AnimatorControllerParameterType.Int:   anim.SetInteger(a.name, a.i); break;
                            case AnimatorControllerParameterType.Bool:
                            case AnimatorControllerParameterType.Trigger:
                                anim.SetBool(a.name, a.b); break;
                        }
                    }

                    if (animatorDeepState && data.animLayers != null)
                    {
                        foreach (var l in data.animLayers)
                            anim.Play(l.stateHash, l.index, Mathf.Repeat(l.normalizedTime, 1f));
                        anim.Update(0f);
                    }
                }
            }

            // NavMeshAgent apply
            if (captureNavMeshAgent && data.nma.has)
            {
                var nma = GetNavMeshAgent();
                if (nma)
                {
                    SetProp(nma, "isStopped",   data.nma.stopped);
                    SetProp(nma, "speed",       data.nma.speed);
                    SetProp(nma, "acceleration",data.nma.accel);
                    SetProp(nma, "angularSpeed",data.nma.angSpeed);
                    SetProp(nma, "destination", data.nma.dest);
                }
            }

            // Custom apply
            if (captureCustomComponents && data.custom != null)
                ApplyCustomComponents(data.custom);
        }

        public override string PrefabKey => prefabKey;

        // ---------- Custom Components (reflection) ----------

        List<CustomCompSnapshot> CaptureCustomComponents()
        {
            var snaps = new List<CustomCompSnapshot>();
            var comps = (onlyTheseComponents != null && onlyTheseComponents.Count > 0)
                      ? onlyTheseComponents.Where(c => c).ToArray()
                      : GetComponents<MonoBehaviour>();

            foreach (var c in comps)
            {
                if (!c) continue;

                // Save systém a ovladač hráče NEukládáme
                if (c is SaveComponent || c is SaveAgent || c is SaveId || c is SaveTag || c is PlayerController)
                    continue;

                if (c.GetType().GetCustomAttribute<SaveIgnoreAttribute>() != null) continue;

                var fields = CollectSerializableFields(c.GetType());
                if (fields.Count == 0) continue;

                var list = new List<FieldSnapshot>();
                foreach (var f in fields)
                {
                    if (f.GetCustomAttribute<SaveIgnoreAttribute>() != null) continue;

                    var val = f.GetValue(c);
                    var declaredType = f.FieldType.AssemblyQualifiedName;

                    // UnityEngine.Object reference → uložit jako SaveId (scénový ref)
                    if (val is UnityEngine.Object uo)
                    {
                        if (uo == null)
                        {
                            list.Add(new FieldSnapshot { name = f.Name, type = declaredType, isRef = true, refId = null, refComponent = null, json = null });
                            continue;
                        }

                        if (uo is Component comp)
                        {
                            var go = comp.gameObject;
                            var sid = go.GetComponent<SaveId>();
                            if (sid)
                            {
                                list.Add(new FieldSnapshot
                                {
                                    name = f.Name,
                                    type = declaredType,
                                    isRef = true,
                                    refId = sid.Id,
                                    refComponent = comp.GetType().AssemblyQualifiedName
                                });
                                continue;
                            }
                        }
                        else if (uo is GameObject go2)
                        {
                            var sid = go2.GetComponent<SaveId>();
                            if (sid)
                            {
                                list.Add(new FieldSnapshot
                                {
                                    name = f.Name,
                                    type = declaredType,
                                    isRef = true,
                                    refId = sid.Id,
                                    refComponent = typeof(GameObject).AssemblyQualifiedName
                                });
                                continue;
                            }
                        }

                        if (!allowUnityObjectRefs) continue;
                        list.Add(new FieldSnapshot
                        {
                            name = f.Name, type = declaredType, isRef = false,
                            json = SaveCodec.ToJson(val)
                        });
                        continue;
                    }

                    // normální (POCO) hodnota
                    list.Add(new FieldSnapshot
                    {
                        name = f.Name,
                        type = declaredType,
                        isRef = false,
                        json = SaveCodec.ToJson(val)
                    });
                }

                if (list.Count > 0)
                {
                    snaps.Add(new CustomCompSnapshot
                    {
                        type = c.GetType().AssemblyQualifiedName,
                        fields = list
                    });
                }
            }

            return snaps;
        }

        void ApplyCustomComponents(List<CustomCompSnapshot> snaps)
        {
            foreach (var snap in snaps)
            {
                var t = Type.GetType(snap.type);
                if (t == null) continue;

                var comp = GetComponent(t) as Component;
                if (!comp) continue;

                var fieldMap = comp.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var fs in snap.fields)
                {
                    var fld = fieldMap.FirstOrDefault(f => f.Name == fs.name);
                    if (fld == null) continue;
                    if (fld.GetCustomAttribute<SaveIgnoreAttribute>() != null) continue;

                    // Reference na scénový objekt → resolve přes SaveManager mapu
                    if (fs.isRef)
                    {
                        object resolved = null;
                        if (!string.IsNullOrEmpty(fs.refId))
                        {
                            var compType = string.IsNullOrEmpty(fs.refComponent) ? null : Type.GetType(fs.refComponent);
                            if (SaveManager.TryResolveSceneObject(fs.refId, compType, out var obj))
                                resolved = obj;
                        }
                        try { fld.SetValue(comp, resolved); } catch { }
                        continue;
                    }

                    // Inline hodnota
                    var fType = Type.GetType(fs.type) ?? fld.FieldType;
                    object val = null;
                    try { val = DeserializeToType(fs.json, fType); } catch { }
                    try { fld.SetValue(comp, val); } catch { }
                }
            }
        }

        static object DeserializeToType(string json, Type t)
        {
            var method = typeof(SaveCodec).GetMethod("FromJson").MakeGenericMethod(t);
            return method.Invoke(null, new object[] { json });
        }

        // ---------- Helpers ----------
        static float Normalize180(float x)
        {
            while (x > 180f) x -= 360f;
            while (x < -180f) x += 360f;
            return x;
        }

        static Type _nmaType; // cache
        Component GetNavMeshAgent()
        {
            if (_nmaType == null)
                _nmaType = Type.GetType("UnityEngine.AI.NavMeshAgent, UnityEngine.AIModule");
            return _nmaType != null ? GetComponent(_nmaType) : null;
        }

        static T GetProp<T>(Component c, string prop)
        {
            var p = c.GetType().GetProperty(prop, BindingFlags.Instance | BindingFlags.Public);
            if (p != null && p.CanRead && p.PropertyType == typeof(T))
                return (T)p.GetValue(c);
            return default;
        }
        static void SetProp<T>(Component c, string prop, T val)
        {
            var p = c.GetType().GetProperty(prop, BindingFlags.Instance | BindingFlags.Public);
            if (p != null && p.CanWrite && p.PropertyType == typeof(T))
                p.SetValue(c, val);
        }

        static bool HasAttr<TA>(FieldInfo f) where TA : Attribute => f.GetCustomAttribute<TA>() != null;

        static List<FieldInfo> CollectSerializableFields(Type t)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fields = t.GetFields(flags);
            var list = new List<FieldInfo>();
            foreach (var f in fields)
            {
                if (f.IsNotSerialized) continue;
                if (HasAttr<SaveIgnoreAttribute>(f)) continue;

                bool unitySerializable = f.IsPublic || f.GetCustomAttribute<SerializeField>() != null
                                         || f.GetCustomAttribute<SaveInlineAttribute>() != null;
#if HAS_ODIN
                bool odinSerializable  = f.GetCustomAttribute<Sirenix.Serialization.OdinSerializeAttribute>() != null;
                if (unitySerializable || odinSerializable) list.Add(f);
#else
                if (unitySerializable) list.Add(f);
#endif
            }
            return list;
        }
    }
}
