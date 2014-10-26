using System.ComponentModel;
using System.Linq;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ReBot.API;
using WarlockCommon;


namespace ReBot
{
    [Rotation("Warlock Demonology - Icy Veins Profile", "Avoloos", WoWClass.Warlock, Specialization.WarlockDemonology, 40)]
    public sealed class Avoloos_WarlockDemonologyIcyVeins : CombatRotation
    {
        [JsonProperty("SelectedPet"), JsonConverter(typeof(StringEnumConverter))]
        public WarlockPet SelectedPet = WarlockPet.AutoSelect;
        [JsonProperty("UsePet")] 
        public bool UsePet = true;

        // If this value is true Hand of Guldan will be cast, else it will not.
        private bool handOfGuldanSpellLock = false;
        private int minMoltenStacksForSoulfire = 5;

        public Avoloos_WarlockDemonologyIcyVeins()
        {
            GroupBuffs = new[]
            {
                "Dark Intent",
                (CurrentBotName == "PvP" ? "Create Soulwell" : null)
            };
            PullSpells = new[]
            {
                "Shadow Bolt"
            };

            UsePet = true;
        }

        public override bool OutOfCombat()
        {
            CastSelf("Fire and Brimstone", () => HasAura("Fire and Brimstone"));
            if (CastSelf("Dark Intent", () => !HasAura("Dark Intent"))) return true;
            if (CastSelf("Unending Breath", () => Me.IsSwimming && !HasAura("Unending Breath"))) return true;
            if (CastSelf("Soulstone", () => CurrentBotName != "Combat" && !HasAura("Soulstone"))) return true;

            if (HasSpell("Grimoire of Sacrifice"))
            {
                if (!HasAura("Grimoire of Sacrifice"))
                {
                    if (this.SummonPet(SelectedPet)) return true;
                    if (CastSelf("Grimoire of Sacrifice", () => Me.HasAlivePet)) return true;
                }
            }
            else if (UsePet)
            {
                if (this.SummonPet(SelectedPet)) return true;
            }
            else if (Me.HasAlivePet)
            {
                Me.PetDismiss();
            }

            if (CastSelfPreventDouble("Create Healthstone", () => Inventory.Healthstone == null, 10000)) return true;

            return false;
        }
        
        private bool CastOnAdds(string spellName, Func<ReBot.API.UnitObject, bool> castCondition)
        {
            foreach(var add in Adds)
            {
                if(Cast(
                    spellName,
                    () => castCondition(add),
                    add
                )) return true;
            }
            return false;
        }
        
        private bool CastOnAdds(string spellName, Func<ReBot.API.UnitObject, bool> castCondition, Func<ReBot.API.UnitObject, bool> addsCondition)
        {
            foreach(var add in Adds.Where(addsCondition))
            {
                if(Cast(
                    spellName,
                    () => castCondition(add),
                    add
                )) return true;
            }
            return false;
        }
        
        private bool HasFelguard()
        {
            return ((Me.Pet.EntryID == 17252) || (Me.Pet.EntryID == 58965));
        }
        
        private bool doMultiTargetRotation(int MobsInFrontOfMe)
        {
            bool doSoulFire = true;
            bool doHellfire = false;
            bool doImmolationAura = false;
            bool doShadowBolt = true;
            
            // Check for Felguard/Wrathguard
            bool doFellstorm = HasFelguard();
            bool doChaosWave = false; // TODO: Support it for easy groups of enemies.
            bool dotAllTargets = false;
            
            if(MobsInFrontOfMe >= 6) {
                // Skip Soul Fire
                doSoulFire = false;
            } else if(MobsInFrontOfMe >= 4) {
                // TODO: Support Mannoroth's Fury
                doHellfire = !Me.HasAura("Metamorphosis");
                doShadowBolt = false;
                minMoltenStacksForSoulfire = 10;
            } else if(MobsInFrontOfMe >= 3) {
                doImmolationAura = Me.HasAura("Metamorphosis");
            } else {
                doFellstorm = false; // I think it would be useless to kick the CD in for less than 3 mobs
                dotAllTargets = true;
            }
            
            // Always do Hand of Gul'dan id available and before tick ends
            // highest prio!
            // TODO: find a way to get the charges of Hands of Guldan and activate it only if 2 charges are there
            //       till we got no charges left, then deactivate till we got 2 again.
            handOfGuldanSpellLock = true;
            if (Cast(
                "Hand of Gul'dan",
                () => handOfGuldanSpellLock && Target.AuraTimeRemaining("Hand of Gul'dan") <= 3f
            )) return true;
            
            if(dotAllTargets)
            { // Do all the Adds dotting.
                if(CastOnAdds(
                    "Corruption",
                    (add) => (add.HpGreaterThanOrElite(0.15) && (!add.HasAura("Corruption") || add.AuraTimeRemaining("Corruption") <= 6f))
                )) return true;
                
                if(Me.HasAura("Metamorphosis"))
                {
                    if(CastOnAdds(
                        "Doom",
                        (add) => (add.HpGreaterThanOrElite(0.15) && (!add.HasAura("Corruption") || add.AuraTimeRemaining("Corruption") <= 18f))
                    )) return true;
                }
            }

            if(Cast(
                "Soul Fire",
                () =>  
                   (Target.HealthFraction <= 0.25)
                || (HasAura("Molten Core", true, minMoltenStacksForSoulfire))
            )) return true;
            
            // TODO: find a way to check if the Felguard is IN a group of adds...
            // But Hand of Gul'dan and Corruption should be more than enough time for the guard to get there.
            if(Cast(
                "Command Demon", 
                () => doFellstorm
            )) return true;
            
            // TODO: find a way to get close to the enemies (leap there?)
            if(CastPreventDouble(
                "Immolation Aura", 
                () => doImmolationAura
            )) return true;
            
            // TODO: find a way to integrate Chaos Wave if targets are easy
            
            // Lets stick to our singleRotaiton if something above does not procc
            return doSingleRotation();
        }
        
        public bool doSingleRotation()
        {
            if(Cast(
                "Corruption",
                () => (Target.HpGreaterThanOrElite(0.15) && (!Target.HasAura("Corruption") || Target.AuraTimeRemaining("Corruption") <= 6f))
            )) return true;
            
            // Always do Hand of Gul'dan id available and before tick ends
            // highest prio!
            // TODO: find a way to get the charges of Hands of Guldan and activate it only if 2 charges are there
            //       till we got no charges left, then deactivate till we got 2 again.
            handOfGuldanSpellLock = true;
            if (Cast(
                "Hand of Gul'dan",
                () => handOfGuldanSpellLock && Target.AuraTimeRemaining("Hand of Gul'dan") <= 3f
            )) return true;
            
            if(Cast(
                "Soul Fire",
                () =>  
                   (Target.HealthFraction <= 0.25)
                || (HasAura("Molten Core", true, minMoltenStacksForSoulfire))
            )) return true;
            
            return false; // Fill with ShadowBolts as they are default
        }

        public override void Combat()
        {
            // reset some vars
            minMoltenStacksForSoulfire = 5;
            
            //no globalcd
            CastSelf("Dark Soul: Knowledge", () => Target.IsInCombatRangeAndLoS);
            Cast("Command Demon", () => (this.IsPetActive("Summon Felhunter") || SelectedPet == WarlockPet.Felhunter) && Target.IsCastingAndInterruptible());
            Cast("Command Demon", () => !HasSpell("Grimoire of Sacrifice") && (this.IsPetActive("Summon Imp") || SelectedPet == WarlockPet.SoulImp) && Me.HealthFraction <= 0.75);
            Cast("Command Demon", () => (this.IsPetActive("Summon Voidwalker") || SelectedPet == WarlockPet.Voidwalker) && Me.HealthFraction < 0.5);
            Cast("Axe Toss", () => HasFelguard() && Target.IsCastingAndInterruptible() ); // Should have no gc as its a pet ability
            
            //Heal
            CastSelf("Unending Resolve", () => Me.HealthFraction <= 0.5); //no gc
            
            if (Me.HasAlivePet)
            {
                UnitObject add = Adds.FirstOrDefault(x => x.Target == Me);
                if (add != null)
                    Me.PetAttack(add);
            }
            
            //Heal
            if (Cast("Mortal Coil", () => Me.HealthFraction <= 0.5)) return;
            if (CastSelf("Dark Regeneration", () => Me.HealthFraction <= 0.6)) return;
            if (Cast("Summon Doomguard", () => Me.HpLessThanOrElite(0.5))) return;
            if (CastPreventDouble("Drain Life", () => Me.HealthFraction < 0.5, 10000)) return;

            
            if (HasAura("Metamorphosis"))
            {
                if (CastSelf(
                    "Metamorphosis", 
                    () => 
                       (Me.GetPower(WoWPowerType.WarlockDemonicFury) < 750 && !HasAura("Dark Soul: Knowledge")) 
                    || (SpellCooldown("Metamorphosis") == 0 && !Me.InCombat)
                )) return;
            }
            else
            {
                // TODO: Tune the Dark Soul condition a bit...
                if (CastSelf(
                    "Metamorphosis", 
                    () => 
                        (Me.GetPower(WoWPowerType.WarlockDemonicFury) >= 900)
                     || (Me.GetPower(WoWPowerType.WarlockDemonicFury) >= 400 && Me.HasAura("Dark Soul: Knowledge"))
                        
                )) return;
            }
            
            // Icy Veins Rotation
            if(Adds.Count > 0)
            { 
                if(doMultiTargetRotation(Adds.Count + 1)) return; // +1 so we have the target in the calculation
            } else {
                if(doSingleRotation()) return;
            }
        
            //if nothing was cast, then cast the good old shadow bolt
            Cast("Shadow Bolt");
        }

        public override bool AfterCombat()
        {
            if (CastSelf("Metamorphosis", () => HasAura("Metamorphosis"))) return true;
            return false;
        }
    }
}