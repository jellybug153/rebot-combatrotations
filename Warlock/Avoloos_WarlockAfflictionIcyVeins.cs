
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ReBot.API;
using WarlockCommon;
using System;
using System.Collections.Generic;

namespace ReBot
{
    public class ExpirableUnitObject
    {
        private DateTime TimeCreated;
        public int ExpiresIn { get; set; } // milliseconds
        public UnitObject Unit { get; set; }
        public ExpirableUnitObject(UnitObject unit, int expire)
        {
            TimeCreated = DateTime.Now; // now
            this.Unit = unit;
            this.ExpiresIn = expire;
        }
        public bool IsExpired()
        {
            return DateTime.Now.Millisecond >= TimeCreated.Millisecond + ExpiresIn;
        }
    }

    [Rotation("Warlock Affliction - Icy Veins Profile", "Avoloos", WoWClass.Warlock, Specialization.WarlockAffliction, 40)]
    public class Avoloos_WarlockAfflictionIcyVeins : CombatRotation
    {
        [JsonProperty("SelectedPet"), JsonConverter(typeof(StringEnumConverter))]
        public WarlockPet SelectedPet = WarlockPet.AutoSelect;
        [JsonProperty("UsePet")]
        public bool UsePet = true;

        [JsonProperty("FearBanTime")]
        public int FearBanTime = 10000;

        List<ExpirableUnitObject> fearTrackingList;

        public Avoloos_WarlockAfflictionIcyVeins()
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
        }


        public override bool OutOfCombat()
        {
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
            foreach (var add in Adds)
            {
                if (Cast(
                    spellName,
                    () => castCondition(add),
                    add
                )) return true;
            }
            return false;
        }

        private bool CastOnAdds(string spellName, Func<ReBot.API.UnitObject, bool> castCondition, Func<ReBot.API.UnitObject, bool> addsCondition)
        {
            foreach (var add in Adds.Where(addsCondition))
            {
                if (Cast(
                    spellName,
                    () => castCondition(add),
                    add
                )) return true;
            }
            return false;
        }

        public override void Combat()
        {
            //no globalcd
            CastSelf("Dark Soul: Misery", () => Target.IsInCombatRangeAndLoS);

            Cast("Command Demon", () => (this.IsPetActive("Summon Felhunter") || SelectedPet == WarlockPet.Felhunter) && Target.IsCastingAndInterruptible());
            Cast("Command Demon", () => !HasSpell("Grimoire of Sacrifice") && (this.IsPetActive("Summon Imp") || SelectedPet == WarlockPet.SoulImp) && Me.HealthFraction <= 0.75);
            Cast("Command Demon", () => (this.IsPetActive("Summon Voidwalker") || SelectedPet == WarlockPet.Voidwalker) && Me.HealthFraction < 0.5);


            CastSelf("Unbound Will", () => !Me.CanParticipateInCombat); //no gc
            CastSelf("Dark Bargain", () => Me.HealthFraction < 0.5); //no gc
            CastSelf("Sacrificial Pact", () => Me.HealthFraction < 0.6); //no gc

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
            
            if (CastSelf("Howl of Terror", () => Target.IsPlayer && Target.DistanceSquared <= 8 * 8 || Adds.Any(x => x.IsPlayer && x.DistanceSquared <= 8 * 8))) return;
            if (Cast("Summon Doomguard", () => Me.HpLessThanOrElite(0.5))) return;
            if (CastSelf("Demonic Rebirth", () => Me.HealthFraction < 0.9 && Target.IsInCombatRangeAndLoS)) return;

            if (Cast("Drain Life", () => Me.HealthFraction <= 0.35 && HasAura("Soulburn"))) return;
            if (Cast("Drain Life", () => Me.HealthFraction <= 0.6 && HasAura("Soulburn"))) return;

            //Adds
            if (Adds.Count > 0)
            {
                if (CurrentBotName == "PvP")
                {
                    fearTrackingList = fearTrackingList.Where(x => !x.IsExpired()).ToList(); // trim the list to all non expired feared objects.

                    if (CastSelf("Howl of Terror", () => Adds.Count(x => x.DistanceSquared < 11 * 11) >= 2))
                    { 
                        foreach(var fearedAdd in Adds.Where(x => x.DistanceSquared < 11 * 11))
                        {
                            fearTrackingList.Add(new ExpirableUnitObject(fearedAdd, FearBanTime));
                        } // Do not fear them again (at least not with feat, howl of terror won't be affected by its fear descision, for the moment...)
                        return;
                    }

                    foreach (var add in Adds.Where(x => x.HasAura("Fear") && fearTrackingList.Count((y) => y.Unit == x) == 0))
                    {
                        // Add feared adds which were not feared by us
                        fearTrackingList.Add(new ExpirableUnitObject(add, FearBanTime));
                    }

                    UnitObject add2 = Adds.FirstOrDefault(x => x.Target != null && x.DistanceSquared <= SpellMaxRangeSq("Fear"));
                    if (add2 != null && add2.DistanceSquared < SpellMaxRangeSq("Fear"))
                    {
                        var add = add2;
                        // Fear only real close targets which attack us
                        if (CastPreventDouble(
                            "Fear", () => 
                                   (!add.HasAura("Fear")                             // its not already feared by other ppl.
                                && !add.HasAura("Howl of Terror")                    // and its not already feared by howl
                                && Target.DistanceSquared <= 6.5 * 6.5               // and its close
                                && add.Target == Me                                  // and its targetting me
                                || add.IsCastingAndInterruptible())                  // or its casting and we can interrupt it with fear :D
                                && fearTrackingList.Count((x) => x.Unit == add) == 0 // and its not banned from fear
                            , add, 1000
                        )) return;
                    }
                }

                if (CastOnTerrain("Shadowfury", Target.Position, () => Adds.Count(x => x.DistanceSquaredTo(Target) <= 12 * 12) > 2)) return;

                /*
                 * Against 5 or more enemies, you will need to start using Soulburn Icon Soulburn with Seed of Corruption Icon Seed of Corruption
                 */
                if(Adds.Count() >= 5)
                {
                    if(CastOnAdds(
                        "Soulburn",
                        (add) => !Me.HasAura("Soulburn")
                    )) return;
                }
                if(Me.HasAura("Soulburn"))
                {
                    if(CastOnAdds(
                        "Seed of Corruption",
                        (add) => !add.HasAura("Seed of Corruption")
                    )) return;
                 }

                 /*
                  * Against 2 enemies, use your normal rotation on one of them and keep your DoTs up on the other.
                  * Against 3 or 4 enemies, keep your DoTs up and cast Drain Soul Icon Drain Soul. 
                  * Against 5 or more enemies, While Seed of Corruption is ticking, you should maintain your DoTs on as many targets as possible.
                  */
                foreach (var add1 in Adds.Where(x => x.IsInCombatRangeAndLoS))
                {
                    if(doDotting(add1)) return;
                }    

                if(Adds.Count() >= 3)
                {
                    /*
                     * Against 3 or 4 enemies, keep your DoTs up and cast Drain Soul Icon Drain Soul. 
                     */
                    if (Cast("Drain Soul")) return;
                }
            }

            // Single DPS
            if(doDotting(Target)) return;
            if (CastPreventDouble(
                "Haunt", 
                () => 
                 //   (Me.GetPower(WoWPowerType.WarlockSoulShards) >= 1 && Target.HpGreaterThanOrElite(0.1) && !Target.HasAura("Haunt"))
                 //|| ()
                    (!Target.HasAura("Haunt") || Me.GetPower(WoWPowerType.WarlockSoulShards) >= 4)
                 && (
                    // TODO: Trinket Procc
                    Target.HealthFraction <= 0.25f // the boss is reaching death
                    || Me.GetPower(WoWPowerType.WarlockSoulShards) > 3 // We capped it (sadly we don't get it when we reached half a shard) (and yes I know whis will get this equation to true as we check against >= 4 above!)
                    || Me.HasAura("Dark Soul: Misery")
                 )
            )) return;

            // TODO: MultiDPS with Haunt, maybe?

            // Okay.. now souldrain :D
            if (Cast("Drain Soul")) return;
        }

        private bool doDotting(UnitObject u)
        {
            if (Cast("Agony", () => Target.HpGreaterThanOrElite(0.3) && (!Target.HasAura("Agony") || Target.AuraTimeRemaining("Unstable Affliction") <= 7f))) return true;
            if (Cast("Corruption", () => Target.HpGreaterThanOrElite(0.15) && (!Target.HasAura("Corruption") || Target.AuraTimeRemaining("Unstable Affliction") <= 5f))) return true;
            if (CastPreventDouble("Unstable Affliction", () => Target.HpGreaterThanOrElite(0.2) && (!Target.HasAura("Unstable Affliction") || Target.AuraTimeRemaining("Unstable Affliction") <= 5f))) return true;
            return false;
        }
    }
}