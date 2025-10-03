namespace Obscurus.Weapons
{
    public interface IAmmoEvents
    {
        // Vyvolej, kdykoli se změní InMagazine, běh reloadu, záměna typu střely apod.
        event System.Action AmmoChanged;
    }
}