using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ReBot.API;
using WarlockCommon;
using System;

namespace ReBot
{
    [Rotation("Warlock Destruction - Icy Veins Profile", "Avoloos", WoWClass.Warlock, Specialization.WarlockDestruction, 40)]
    public class Avoloos_WarlockDestructionIcyVeins : CombatRotation
	{
        [JsonProperty("SelectedPet"), JsonConverter(typeof(StringEnumConverter))]
        public WarlockPet SelectedPet = WarlockPet.AutoSelect;
        [JsonProperty("UsePet")]
        public bool UsePet = true;

        public Avoloos_WarlockDestructionIcyVeins()
		{
            GroupBuffs = new[]
			{
				"Dark Intent",
				(CurrentBotName == "PvP" ? "Create Soulwell" : null)
			};
			PullSpells = new[]
			{
				"Conflagrate"
			};
            UsePet = true;

            //Info("Best class on earth YEAH");
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
                if (CastSelf("Flames of Xoroth", () => !Me.HasAlivePet && Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers) >= 1)) return true;
                if (this.SummonPet(SelectedPet)) return true;
            }
            else if (Me.HasAlivePet)
            {
                Me.PetDismiss();
            }


			if (CastSelfPreventDouble("Create Healthstone", () => Inventory.Healthstone == null, 10000)) return true;

			return false;
		}

		public override bool AfterCombat()
		{
			if (CastSelf("Fire and Brimstone", () => HasAura("Fire and Brimstone"))) return true;
			return false;
		}

        public bool doSomeGlobalStuff()
        {
            //no globalcd
            CastSelf("Dark Soul: Instability", () => Target.IsInCombatRangeAndLoS);
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

            //GLOBAL CD CHECK
            if (HasGlobalCooldown())
                return true;

            return false;
        }

        public bool doSomePetAndHealingStuff()
        {
            if (Cast("Mortal Coil", () => Me.HealthFraction <= 0.5)) return true;
            if (CastSelf("Dark Regeneration", () => Me.HealthFraction <= 0.6)) return true;
            if (CastSelf("Flames of Xoroth", () => !HasSpell("Grimoire of Sacrifice") && !Me.HasAlivePet && Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers) >= 1)) return true;

            if (CastSelf("Howl of Terror", () => Target.IsPlayer && Target.DistanceSquared <= 8 * 8 || Adds.Any(x => x.IsPlayer && x.DistanceSquared <= 8 * 8))) return true;
            if (Cast("Summon Doomguard", () => Me.HpLessThanOrElite(0.5))) return true;
            if (CastSelf("Demonic Rebirth", () => Me.HealthFraction < 0.9 && Target.IsInCombatRangeAndLoS)) return true;

            if (CastSelf("Ember Tap", () => Me.HealthFraction <= 0.35 && Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers) >= 1)) return true;

            return false;
        }

        private bool doPvPThings()
        {
            if (CastSelf("Howl of Terror", () => Adds.Count(x => x.DistanceSquared < 11 * 11) >= 2))
                return true;

            UnitObject add = Adds.FirstOrDefault(x => x.Target != null && x.DistanceSquared <= SpellMaxRangeSq("Fear"));
            if (add != null && add.DistanceSquared <= SpellMaxRangeSq("Fear"))
            {
                if (CastPreventDouble("Fear", () => !add.HasAura("Fear") && (!HasSpell("Howl of Terror") || !add.HasAura("Howl of Terror")), add, 8000)) return true;
            }
            return false;
        }

        private bool CastOnAdds(string spellName)
        {
            foreach (var add in Adds)
            {
                if (Cast(
                    spellName,
                    add
                )) return true;
            }
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

        private bool CastPreventDoubleOnAdds(string spellName, Func<ReBot.API.UnitObject, bool> castCondition)
        {
            foreach (var add in Adds)
            {
                if (CastPreventDouble(
                    spellName,
                    () => castCondition(add),
                    add
                )) return true;
            }
            return false;
        }

        private bool CastPreventDoubleOnAdds(string spellName, Func<ReBot.API.UnitObject, bool> castCondition, Func<ReBot.API.UnitObject, bool> addsCondition)
        {
            foreach (var add in Adds.Where(addsCondition))
            {
                if (CastPreventDouble(
                    spellName,
                    () => castCondition(add),
                    add
                )) return true;
            }
            return false;
        }

        private bool doMultitargetRotation()
        {
            if (CurrentBotName == "PvP" && doPvPThings()) return true;
            int burningEmbers = Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers);
            
            /*if (SpellCooldown("Havoc") == 0)
            {
                UnitObject add = Adds.FirstOrDefault();
                if (add != null && add.DistanceSquared <= SpellMaxRangeSq("Havoc"))
                {
                    //havoc on first add, if not already
                    if (Cast("Havoc", () => !add.HasAura("Havoc"), add))
                        return true;
                }
            }*/ // We handle this when doing chaos bolt/shadowburn action




            if (CastOnTerrain("Shadowfury", Target.Position, () => Adds.Count(x => x.DistanceSquaredTo(Target) <= 12 * 12) > 2)) return true;
           
            // Priority #1
            if (HasSpell("Aftermath") && CastOnTerrainPreventDouble("Rain of Fire", Target.Position, null, 7500)) return true;

            var addsCount = Adds.Count();
            // Priority #2
            if (
                   addsCount >= 3                                           // Got a Group
                && ((Me.Pet.EntryID == 17252) || (Me.Pet.EntryID == 58965)) // and Has a Felguard
                && Cast("Command Demon") && HasGlobalCooldown()             // Lets rumble
            ) return true;

            if (
                SpellCooldown("Havoc") == 0 && burningEmbers >= 1 && addsCount < 11
            )
            {
                // Dont waste Havoc apply it to one of the mid-enemies (high max health, low current health)
                var havocAdd = Adds.OrderBy(x => x.Health).FirstOrDefault(x => x.HealthFraction <= 0.4f);
                if(havocAdd == null) havocAdd = Adds.FirstOrDefault(x => x.HealthFraction <= 1f);
                if (Cast("Havoc", havocAdd)) return true;
            }

            // cast Chaosbolt or shadowburn on target as soon as possible and if feasible
            if (Adds.Count(x => x.HasAura("Havoc", true)) > 0)
            {
                if (Cast("Shadowburn", () => addsCount < 11)) return true; // 12 is the border, 11 adds + 1 target = 12
                if (Cast("Chaos Bolt", () => addsCount < 5)) return true;  // 6 is the border, 5 adds + 1 target = 6
            }

            // Priority #3
            if (   HasSpell("Fire and Brimstone") 
                && (
                       (burningEmbers >= 2 && Adds.Count(x => x.DistanceSquaredTo(Target) <= 12 * 12) > 2 )
                    || (burningEmbers >= 1 && Adds.Count(x => x.DistanceSquaredTo(Target) <= 12 * 12) >= 10)
                )
            )
            {
                // Ensure Fire and Brimstone!
                CastSelf("Fire and Brimstone", () => !HasAura("Fire and Brimstone"));

                if (CastOnAdds("Conflagrate")) return true;
                if (CastPreventDoubleOnAdds("Immolate", (add) => Adds.Count(y => !y.HasAura("Immolate") && y.HpLessThanOrElite(0.15)) > 2)) return true;
                if (CastOnAdds("Incinerate")) return true;
            }

            return false;
        }

		public override void Combat()
		{
			//if (Me.IsCasting) useImmolate++; // ?? what ?
		    
            if (doSomeGlobalStuff()) return;
		    if (doSomePetAndHealingStuff()) return;

			int burningEmbers = Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers);

            // MultiTarget Rotation
            if (Adds.Count > 0 && doMultitargetRotation()) return;

            // No Multitarget please
            CastSelf("Fire and Brimstone", () => HasAura("Fire and Brimstone"));

            // Priority #1
            if (Cast(
                    "Shadowburn", 
                    () => 
                           Target.HealthFraction <= 0.2
                           && (
                               Me.HasAura("Dark Soul: Instability")
                            || burningEmbers == 4 // No cast time so 4 is good enough!
                           )
                        
            )) return;

            // Priority #2
            if (CastPreventDouble(
                "Immolate", 
                () =>
                    !Target.HasAura("Immolate", true) 
                 || Target.AuraTimeRemaining("Immolate") <= 4.5f
            )) return;

            // Priority #3
            if (Cast("Conflagrate")) return; // No Stack detection sadly... otherwise we would check for 2

            // Priority #4
		    if (Cast(
		        "Chaos Bolt",
		        () =>
		            Me.HasAura("Dark Soul: Instability")
		            || burningEmbers >= 3
		        // because we don't know about .5 fractions of embers... sadly... But it fullifies the T16 4-piece boni
		        )) return;

            // Priority #5
            // Refresh Immolate is already done in P#2

            // Priority #6
            // TODO: remember old cast position and check with target position and radius so we recast it when he gets out of the rain
            if (HasSpell("Aftermath") && CastOnTerrainPreventDouble("Rain of Fire", Target.Position, null, 7500)) return;

            // Priority #7
            // Conflagrate is already done in #3
            // No Stack detection sadly... otherwise we would check for 1

            // Priority #8
            if (Cast("Incinerate")) return;
		}
	}
}