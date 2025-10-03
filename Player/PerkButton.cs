using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PerkButton : MonoBehaviour
{
    public AlchemyTreeShop shop;
    public PerkId perk;

    Button _btn;

    void Awake() { _btn = GetComponent<Button>(); }

    void Reset()
    {
        _btn = GetComponent<Button>();
        // zkus auto najít shop v rodičích
        if (!shop) shop = GetComponentInParent<AlchemyTreeShop>();
    }

    void OnEnable()
    {
        if (_btn != null) _btn.onClick.AddListener(HandleClick);
    }

    void OnDisable()
    {
        if (_btn != null) _btn.onClick.RemoveListener(HandleClick);
    }

    void HandleClick()
    {
        if (shop) shop.TryBuy(perk);
    }
}