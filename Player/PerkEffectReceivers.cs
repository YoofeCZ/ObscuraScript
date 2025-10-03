// PerkEffectReceivers.cs
using UnityEngine;

public interface IArmorShredReceiver   { void ApplyArmorShred(float value, GameObject source); }
public interface IWeakSpotRevealReceiver { void RevealWeakSpot(float seconds, GameObject source); }
public interface IDotReceiver          { void ApplyDot(float dps, float seconds, GameObject source); }
public interface IAcidPoolReceiver     { void LeaveAcidPool(Vector3 point, float seconds, GameObject source); }