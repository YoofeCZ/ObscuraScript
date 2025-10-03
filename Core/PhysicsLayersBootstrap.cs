using System;
using System.Linq;
using UnityEngine;

public class PhysicsLayersBootstrap : MonoBehaviour
{
    // Pomůcka: bezpečně zjistí index vrstvy podle jména
    int L(string n)
    {
        int id = LayerMask.NameToLayer(n);
        if (id == -1) Debug.LogWarning($"[PhysicsLayersBootstrap] Vrstva '{n}' neexistuje (Project Settings → Tags and Layers).");
        return id;
    }

    void Awake()
    {
        // Pokud některá vrstva chybí, jen zalogujeme – ostatní pravidla se i tak uplatní.
        // Nejprve NonBlockingFX ignoruje VŠECHNY
        int nbfx = L("NonBlockingFX");
        if (nbfx != -1)
        {
            for (int i = 0; i < 32; i++)
                Physics.IgnoreLayerCollision(nbfx, i, true);
        }

        // Zkrácená utilita
        void Set(string a, string b, bool collides)
        {
            int A = L(a), B = L(b);
            if (A == -1 || B == -1) return;
            Physics.IgnoreLayerCollision(A, B, !collides);
        }

        // Tabulka dle doporučení:
        // Player
        Set("Player", "Enemy",          true);
        Set("Player", "Interactable",   true);
        Set("Player", "Pickup",         true);
        Set("Player", "Projectile",     false);
        Set("Player", "AstralOnly",     false);
        Set("Player", "RealOnly",       true);
        Set("Player", "Triggers",       true);

        // Enemy
        Set("Enemy", "Interactable",    true);
        Set("Enemy", "Pickup",          false);
        Set("Enemy", "Projectile",      true);
        Set("Enemy", "AstralOnly",      false);
        Set("Enemy", "RealOnly",        true);
        Set("Enemy", "Triggers",        true);

        // Interactable
        Set("Interactable", "Pickup",   true);
        Set("Interactable", "Projectile", false);
        Set("Interactable", "AstralOnly", false);
        Set("Interactable", "RealOnly", true);
        Set("Interactable", "Triggers", true);

        // Pickup
        Set("Pickup", "Projectile",     false);
        Set("Pickup", "AstralOnly",     false);
        Set("Pickup", "RealOnly",       true);
        Set("Pickup", "Triggers",       true);

        // Projectile
        Set("Projectile", "AstralOnly", true);
        Set("Projectile", "RealOnly",   true);
        Set("Projectile", "Triggers",   false);

        // AstralOnly
        Set("AstralOnly", "RealOnly",   false);
        Set("AstralOnly", "Triggers",   true);

        // RealOnly
        Set("RealOnly", "Triggers",     true);

        // Pozn.: Default, TransparentFX, UI, Water, Ignore Raycast necháváme na výchozích pravidlech Unity.
    }
}
