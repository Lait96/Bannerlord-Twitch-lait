$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot '../BannerlordTwitch/BLTAdoptAHero/Behaviors/BLTFormationTestBehavior.cs') -Raw
$method = [regex]::Match($source, '(?s)private static int Ammo\(Agent agent\).*?return total;\s*}').Value
if (!$method) { throw 'Production ammo method not found' }
$fixture = @'
public enum EquipmentIndex { WeaponItemBeginSlot, Slot1, Slot2, Slot3, NumAllWeaponSlots }
public struct Weapon {
    public bool IsEmpty, Consumable; public int Amount, Ammo, Ranged = -1;
    public Weapon() { }
    public bool IsAnyConsumable() => Consumable;
    public int GetRangedUsageIndex() => Ranged;
}
public class Agent { public Weapons Equipment = new Weapons(); }
public class Weapons {
    public Weapon[] Slots = new Weapon[4];
    public Weapon this[EquipmentIndex index] => Slots[(int)index];
}
public class RunnerAmmoTest {
METHOD
    public static int Count(Agent a) => Ammo(a);
}
'@
Add-Type -TypeDefinition $fixture.Replace('METHOD', $method)
function Check-Ammo($weapons, $expected, $name) {
    $agent = [Agent]::new()
    $agent.Equipment.Slots = $weapons
    $actual = [RunnerAmmoTest]::Count($agent)
    if ($actual -ne $expected) { throw "$name expected $expected, got $actual" }
}
function Weapon($empty, $consumable, $amount, $ammo, $ranged) {
    $w = [Weapon]::new()
    $w.IsEmpty = $empty; $w.Consumable = $consumable; $w.Amount = $amount; $w.Ammo = $ammo; $w.Ranged = $ranged
    return $w
}
$empty = Weapon $true $false 0 0 -1
Check-Ammo @($empty,$empty,$empty,$empty) 0 'empty inventory'
Check-Ammo @((Weapon $false $true 20 0 -1),(Weapon $false $false 1 1 0),$empty,$empty) 21 'crossbow bolt bag plus loaded bolt'
Check-Ammo @((Weapon $false $true 3 3 0),$empty,$empty,$empty) 3 'throwables counted once'
Check-Ammo @((Weapon $false $true 0 0 0),(Weapon $false $true 4 4 0),$empty,$empty) 4 'empty first throwing stack'
Check-Ammo @((Weapon $false $false 1 0 -1),$empty,$empty,$empty) 0 'melee weapon excluded'
Check-Ammo @((Weapon $false $true 12 0 -1),(Weapon $false $true 8 0 -1),(Weapon $false $false 1 0 0),$empty) 20 'multiple ammo bags'
'6 production observer ammunition checks passed'
