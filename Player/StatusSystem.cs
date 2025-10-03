using UnityEngine;
using System.Collections.Generic;

public class StatusSystem : MonoBehaviour
{
    class Active
    {
        public string id;
        public float tLeft;
    }

    readonly List<Active> _actives = new();

    public void ApplyDebuff(GameObject target, string debuffId, float baseDuration)
    {
        float duration = baseDuration;
        var perks = target ? target.GetComponent<AlchemyPerks>() : null;
        if (perks) duration = perks.ModifyStatusDuration(duration);   // Calm Nerves: −% trvání

        _actives.Add(new Active { id = debuffId, tLeft = Mathf.Max(0.05f, duration) });
        // TODO: FX/UI ikony
    }

    void Update()
    {
        for (int i = _actives.Count - 1; i >= 0; i--)
        {
            _actives[i].tLeft -= Time.deltaTime;
            if (_actives[i].tLeft <= 0f) _actives.RemoveAt(i);
        }
    }
}