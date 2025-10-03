using UnityEngine;
using Obscurus.Player;
using Obscurus.Weapons; // jen kvůli typu IWeapon v eventu (WeaponChanged)

public class WeaponWallBlockerBox : MonoBehaviour
{
    public enum DetectionMode { CameraSphere, ProbeOverlap, Both }

    [Header("References (auto)")]
    public WeaponHolder holder;
    public Transform weaponPivot;     // nastav v Inspectoru (doporučeno). Jinak se doplní automaticky.
    public Camera cam;                // hráčská kamera

    [Header("World mask")]
    public LayerMask worldMask = ~0;

    [Header("Detekce – CameraSphere")]
    public DetectionMode mode = DetectionMode.Both;
    public float sphereRadius = 0.18f;
    public float startDistance = 0.55f;
    public float insideRadius = 0.20f;
    public float feelerSide = 0.18f;
    public float feelerUp = 0.10f;

    [Header("Detekce – ProbeOverlap (sonda u hlavně)")]
    public Transform probe;                 // plní se při equipu
    public float probeRadius = 0.12f;
    public Vector3 probeLocalOffset = Vector3.zero;

    [Tooltip("Při equipu se pokusit sondu najít/vytvořit automaticky")]
    public bool autoFindProbe = true;
    public bool autoCreateProbeIfMissing = true;
    public string probeName = "Probe";
    public string muzzleName = "Muzzle";

    [Header("Zasunutí zbraně")]
    public Vector3 maxOffset = new Vector3(0f, -0.10f, -0.28f);

    [Header("Stabilita (proti poskakování)")]
    [Tooltip("Kolik METRŮ navíc musí být volno, aby se zbraň „odlatchovala“.")]
    public float hysteresisMeters = 0.06f;
    [Tooltip("Rychlost ZASOUVÁNÍ do překážky.")]
    public float pushLerp = 18f;
    [Tooltip("Rychlost VYSOUVÁNÍ zpět.")]
    public float releaseLerp = 7f;
    [Tooltip("Mrtvá zóna diference T – potlačí drobné změny.")]
    [Range(0f, 0.2f)] public float tDeadZone = 0.02f;
    [Tooltip("Minimální doba (s) po zachycení, po kterou držíme latch.")]
    public float minBlockTime = 0.06f;
    [Tooltip("Po jak dlouho ještě držet plný zásah, když sonda právě přestala kolidovat.")]
    public float probeReleaseDelay = 0.06f;

    [Header("Smoothing výsledného posuvu")]
    public float baseLerpSpeed = 16f;

    [Header("Debug")]
    public bool drawGizmos = true;
    public bool showHudInfo = false;

    // runtime
    Vector3 _baseLocal, _smoothed;
    string _lastInfo = "<none>";
    float  _lastDist = -1f;
    bool   _hadHit = false;
    Vector3 _lastHitPoint = Vector3.zero;

    // stabilita
    float _tSmoothed;         // smoothed „síla“ zasunutí (0–1)
    bool  _latched;           // jsme v režimu „držím, dokud není víc místa“
    float _latchedAt;         // čas, kdy jsme se naposled „chytli“
    float _probeHold;         // countdown pro držení proby po uvolnění

    void Awake()
    {
        if (!holder) holder = GetComponentInParent<WeaponHolder>() ?? WeaponHolder.Local;

        // --- PIVOT: žádný weaponAnchor už neexistuje, takže:
        if (!weaponPivot)
        {
            // zkusit parent aktuální zbraně (pokud už je nějaká equipnutá)
            MonoBehaviour curMb = null;
            if (holder != null && holder.Current is MonoBehaviour m) curMb = m;

            if (curMb && curMb.transform.parent)
                weaponPivot = curMb.transform.parent;
            else
                weaponPivot = transform; // poslední fallback
        }

        if (!cam)
        {
            cam = Camera.main;
            if (!cam) cam = GetComponentInParent<Camera>();
        }
        if (!cam)
        {
            Debug.LogError("[WeaponWallBlockerBox] Cam is NULL – přiřaď kameru v Inspectoru.");
            enabled = false;
            return;
        }

        _baseLocal = weaponPivot.localPosition;
    }

    void OnEnable()
    {
        if (!holder) holder = GetComponentInParent<WeaponHolder>() ?? WeaponHolder.Local;
        if (holder) holder.WeaponChanged += OnWeaponChanged;

        // pokud je už nějaká zbraň equipnutá při startu scény
        if (holder && holder.Current != null) OnWeaponChanged(holder.Current);
    }

    void OnDisable()
    {
        if (holder) holder.WeaponChanged -= OnWeaponChanged;
    }

    void OnWeaponChanged(IWeapon w)
    {
        probe = null;

        // Pokud pivot chybí, doplň ho z právě aktivní zbraně (její parent bývá rig/pivot).
        if (!weaponPivot && w is MonoBehaviour mbW)
        {
            weaponPivot = mbW.transform.parent ? mbW.transform.parent : mbW.transform;
            _baseLocal = weaponPivot.localPosition; // znovu načti základ
        }

        if (!autoFindProbe || w == null) return;

        var mb = w as MonoBehaviour;
        if (!mb) return;

        // 1) přesný název "Probe"
        probe = FindChildByName(mb.transform, probeName);
        if (probe) return;

        // 2) "Muzzle" / cokoliv co obsahuje "muzzle"
        var muzz = FindChildByName(mb.transform, muzzleName);
        if (!muzz) muzz = FindChildByPartial(mb.transform, "muzzle");

        // 3) vytvoř pouze ZA BĚHU hry (ne v editaci/prefabu)
        if (muzz && Application.isPlaying && autoCreateProbeIfMissing)
        {
            var go = new GameObject(probeName);
            var t = go.transform;
            t.SetParent(muzz, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            probe = t;
        }

        // když se nepodařilo a mód sondu vyžaduje, degrade na CameraSphere
        if ((mode == DetectionMode.ProbeOverlap || mode == DetectionMode.Both) && !probe)
        {
            Debug.LogWarning("[WeaponWallBlockerBox] Probe nebyla nalezena ani vytvořena – přepínám na CameraSphere.");
            mode = DetectionMode.CameraSphere;
        }
    }

    Transform FindChildByName(Transform root, string exact)
    {
        if (!root || string.IsNullOrEmpty(exact)) return null;
        var all = root.GetComponentsInChildren<Transform>(true);
        var cmp = System.StringComparison.OrdinalIgnoreCase;
        for (int i = 0; i < all.Length; i++)
            if (all[i] && string.Equals(all[i].name, exact, cmp)) return all[i];
        return null;
    }

    Transform FindChildByPartial(Transform root, string contains)
    {
        if (!root || string.IsNullOrEmpty(contains)) return null;
        var all = root.GetComponentsInChildren<Transform>(true);
        var cmp = System.StringComparison.OrdinalIgnoreCase;
        for (int i = 0; i < all.Length; i++)
            if (all[i] && all[i].name.IndexOf(contains, cmp) >= 0) return all[i];
        return null;
    }

    void LateUpdate()
    {
        // --- 1) Detekce s hysterezí ---
        float startForThisFrame = startDistance + (_latched ? hysteresisMeters : 0f);

        float tCamera = 0f, tProbe = 0f;
        _hadHit = false;
        _lastInfo = "<none>";
        _lastDist = -1f;

        if (mode == DetectionMode.CameraSphere || mode == DetectionMode.Both)
            tCamera = DetectByCameraSphere(startForThisFrame);

        if (mode == DetectionMode.ProbeOverlap || mode == DetectionMode.Both)
            tProbe = DetectByProbe();

        float tRaw = Mathf.Clamp01(Mathf.Max(tCamera, tProbe));

        // latch logika (brání okamžitému odskoku)
        if (!_latched && tRaw > 0.001f) { _latched = true; _latchedAt = Time.time; }
        if (_latched && tRaw <= 0.0001f && (Time.time - _latchedAt) >= minBlockTime)
            _latched = false;

        // --- 2) Dead-zone + různé rychlosti nahoru/dolů ---
        float target = tRaw;
        if (Mathf.Abs(target - _tSmoothed) < tDeadZone)
            target = _tSmoothed;

        float lerp = (target > _tSmoothed) ? pushLerp : releaseLerp;
        _tSmoothed = Mathf.Lerp(_tSmoothed, target, Time.deltaTime * Mathf.Max(1f, lerp));

        // --- 3) Posun v osách kamery -> do lokálu pivotu
        Vector3 worldOffset =
            cam.transform.right   * (maxOffset.x * _tSmoothed) +
            cam.transform.up      * (maxOffset.y * _tSmoothed) +
            cam.transform.forward * (maxOffset.z * _tSmoothed);

        Transform basis = weaponPivot && weaponPivot.parent ? weaponPivot.parent : weaponPivot;
        if (!basis) basis = transform;

        Vector3 localOffset = basis.InverseTransformVector(worldOffset);

        _smoothed = Vector3.Lerp(_smoothed, _baseLocal + localOffset, Time.deltaTime * baseLerpSpeed);
        if (weaponPivot) weaponPivot.localPosition = _smoothed;
    }

    // ---------- Detection implementations ----------
    float DetectByCameraSphere(float useStartDistance)
    {
        Vector3 origin = cam.transform.position;
        Vector3 fwd    = cam.transform.forward;

        Vector3 upOff  = cam.transform.up * feelerUp;
        Vector3 right  = cam.transform.right;

        Vector3[] origins = new Vector3[5];
        origins[0] = origin + upOff;                       // center
        origins[1] = origin + upOff + right *  feelerSide; // right
        origins[2] = origin + upOff + right * -feelerSide; // left
        origins[3] = origin + cam.transform.up * (feelerUp * 2f); // up
        origins[4] = origin;                               // raw origin

        float bestT = 0f;
        float minDist = float.PositiveInfinity;
        RaycastHit bestHit = new RaycastHit();
        bool any = false;

        for (int i = 0; i < origins.Length; i++)
        {
            RaycastHit hit;
            if (Physics.SphereCast(origins[i], sphereRadius, fwd, out hit, useStartDistance, worldMask, QueryTriggerInteraction.Ignore))
            {
                any = true;
                if (hit.distance < minDist)
                {
                    minDist = hit.distance;
                    bestHit = hit;
                }
            }
        }

        if (any)
        {
            _hadHit = true;
            _lastInfo = bestHit.collider ? bestHit.collider.name : "<null>";
            _lastDist = bestHit.distance;
            _lastHitPoint = bestHit.point;

            // mapujeme na 0..1 podle useStartDistance (větší, když jsme latched)
            bestT = 1f - Mathf.Clamp01(bestHit.distance / useStartDistance);
        }
        else
        {
            // fallback: jsme-li už "uvnitř"
            Vector3 checkPos = origin + fwd * (sphereRadius * 0.5f);
            if (Physics.CheckSphere(checkPos, insideRadius, worldMask, QueryTriggerInteraction.Ignore))
            {
                _hadHit = true;
                _lastInfo = "(inside)";
                _lastDist = 0f;
                _lastHitPoint = checkPos;
                bestT = 1f;
            }
        }
        return bestT;
    }

    float DetectByProbe()
    {
        if (!probe) return 0f;

        Vector3 center = probe.TransformPoint(probeLocalOffset);
        Collider[] buf = new Collider[8];
        int count = Physics.OverlapSphereNonAlloc(center, probeRadius, buf, worldMask, QueryTriggerInteraction.Ignore);

        if (count > 0)
        {
            _probeHold = probeReleaseDelay; // drž ještě chvilku i kdyby to hned zmizelo
            _hadHit = true;
            _lastInfo = buf[0] ? buf[0].name : "<null>";
            _lastDist = 0f;
            _lastHitPoint = center;
            return 1f; // u sondy chceme plný zásun
        }

        if (_probeHold > 0f)
        {
            _probeHold -= Time.deltaTime;
            _hadHit = true;
            _lastInfo = "(probe hold)";
            _lastDist = 0f;
            _lastHitPoint = center;
            return 1f;
        }

        return 0f;
    }

    // ---------- Debug ----------
    void OnGUI()
    {
        if (!showHudInfo) return;
        GUIStyle style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 12 };
        Rect r = new Rect(10, 10, 520, 90);
        string txt = "[WeaponWallBlockerBox]\n";
        txt += $"Cam: {(cam ? cam.name : "<null>")}   Mode: {mode}\n";
        txt += $"tSmoothed: {_tSmoothed:F2}   Latched: {_latched}   Mask: {worldMask.value}\n";
        txt += "Hit: " + (_hadHit ? _lastInfo : "<none>") +
               "   Dist: " + (_lastDist >= 0f ? _lastDist.ToString("F3") : "-");
        GUI.Box(r, txt, style);
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Camera gCam = cam ? cam : Camera.main;
        if (gCam)
        {
            Vector3 origin = gCam.transform.position;
            Vector3 fwd    = gCam.transform.forward;

            Gizmos.color = new Color(1f, 0.6f, 0.15f, 0.8f);
            Gizmos.DrawWireSphere(origin + fwd * startDistance, sphereRadius);

            // „unlatch“ hranice (startDistance + hysteresis)
            Gizmos.color = new Color(0.6f, 1f, 0.2f, 0.7f);
            Gizmos.DrawWireSphere(origin + fwd * (startDistance + hysteresisMeters), sphereRadius);

            Gizmos.color = new Color(1f, 1f, 0f, 0.9f);
            Vector3 upOff = gCam.transform.up * feelerUp;
            Vector3 right = gCam.transform.right;
            Gizmos.DrawWireSphere(origin + upOff, sphereRadius);
            Gizmos.DrawWireSphere(origin + upOff + right *  feelerSide, sphereRadius);
            Gizmos.DrawWireSphere(origin + upOff + right * -feelerSide, sphereRadius);
            Gizmos.DrawWireSphere(origin + gCam.transform.up * (feelerUp * 2f), sphereRadius);

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(origin + fwd * (sphereRadius * 0.5f), insideRadius);
        }

        if (probe)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(probe.TransformPoint(probeLocalOffset), probeRadius);
        }

        if (_hadHit)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_lastHitPoint, 0.02f);
        }
    }
}
