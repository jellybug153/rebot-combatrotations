using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ReBot.API;
using WarlockCommon;
using System;
using System.Collections.Generic;

namespace Avoloos
{
    /// <summary>
    /// This class represents an Object, which can expire.
    /// </summary>
    public class ExpirableObject
    {
        /// <summary>
        /// The time where the <see cref="Avoloos.ExpirableObject"/> was created created.
        /// </summary>
        DateTime TimeCreated;

        /// <summary>
        /// Gets or sets the expires in milliseconds.
        /// </summary>
        /// <value>The expires in given milliseconds.</value>
        public int ExpiresIn { get; set; }

        /// <summary>
        /// Gets or sets the expiring object.
        /// </summary>
        /// <value>The expiring object.</value>
        public object ExpiringObject { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Avoloos.ExpirableObject"/> class.
        /// </summary>
        /// <param name="expiringObject">The object which can expire.</param>
        /// <param name="expire">The time in milliseconds in which the given object will expire</param>
        public ExpirableObject(object expiringObject, int expire)
        {
            TimeCreated = DateTime.Now;
            ExpiringObject = expiringObject;
            ExpiresIn = expire;
        }

        /// <summary>
        /// Determines whether this instance is expired.
        /// </summary>
        /// <returns><c>true</c> if this instance is expired; otherwise, <c>false</c>.</returns>
        public bool IsExpired()
        {
            return DateTime.Now.Millisecond >= TimeCreated.Millisecond + ExpiresIn;
        }

        /// <summary>
        /// Will reset the expire timer, so the object will be again valid as set in the creation.
        /// </summary>
        public void ResetTime()
        {
            TimeCreated = DateTime.Now;
        }
    }
}
    
namespace Avoloos
{
    namespace Warlock
    {
        /// <summary>
        /// Basic class which implements some convinience functions.
        /// </summary>
        abstract public class WarlockBaseRotation : CombatRotation
        {
            /// <summary>
            /// The selected pet.
            /// </summary>
            [JsonProperty("SelectedPet"), JsonConverter(typeof(StringEnumConverter))]
            public WarlockPet SelectedPet = WarlockPet.AutoSelect;

            /// <summary>
            /// Should use pet?
            /// </summary>
            [JsonProperty("UsePet")]
            public bool UsePet = true;

            /// <summary>
            /// Should the bot use Terrorguard/Infernal
            /// </summary>
            [JsonProperty("UseAdditionalDPSPet")]
            public bool UseAdditionalDPSPet = true;

            /// <summary>
            /// Should the Bot fear?
            /// </summary>
            [JsonProperty("PvP-DoFear")]
            public bool FearDoFear = true;

            /// <summary>
            /// The fear ban time.
            /// </summary>
            [JsonProperty("PvP-FearBanTime")]
            public int FearBanTime = 10000;
            
            /// <summary>
            /// The fear tracking list.
            /// </summary>
            protected List<ExpirableObject> FearTrackingList;

            /// <summary>
            /// Initializes a new instance of the <see cref="Avoloos.Warlock.WarlockBaseRotation"/> class.
            /// </summary>
            protected WarlockBaseRotation()
            {
                FearTrackingList = new List<ExpirableObject>();
            }

            /// <summary>
            /// This will try to cast a fear spell. 
            /// 
            /// It will hereby utilize a FearBanList for players, so it won't try to fear all the time.
            /// </summary>
            /// <returns><c>true</c>, if a fear spell was cast, <c>false</c> otherwise.</returns>
            protected bool CastFearIfFeasible()
            {
                if (!FearDoFear)
                    return false;
                
                try {
                    FearTrackingList = FearTrackingList.Where(x => !x.IsExpired()).ToList(); // trim the list to all non expired feared objects.

                    var FearableAdds = Adds.Where(x => x.DistanceSquared < 11 * 11);
                    if (CastSelf("Howl of Terror", () => FearableAdds.Count() >= 2)) { 
                        foreach (var fearedAdd in FearableAdds.Where(x => x.IsPlayer)) {
                            FearTrackingList.Add(new ExpirableObject(fearedAdd, FearBanTime));
                        } // Do not fear them again (at least not with feat, howl of terror won't be affected by its fear descision, for the moment...)
                        return true;
                    }

                    foreach (var add in Adds.Where(x => x.IsPlayer && x.HasAura("Fear"))) {
                        // Add feared adds which were not feared by us
                        //if(FearTrackingList.Count(y => y.Unit == x) == 0)
                        var alreadyAdded = FearTrackingList.FirstOrDefault(y => add.Equals(y.ExpiringObject));
                        if (alreadyAdded != null) {
                            alreadyAdded.ResetTime();
                        } else {
                            FearTrackingList.Add(new ExpirableObject(add, FearBanTime));
                        }
                    }
                } catch (Exception e) { // catch everything
                    API.PrintError(
                        "Got an error in fear management logic. Disabling fear for now... Please Report to Avoloos.",
                        e
                    );
                    FearDoFear = false;
                }

                UnitObject add2 = Adds.FirstOrDefault(x => x.Target != null && x.DistanceSquared <= SpellMaxRangeSq("Fear"));
                if (add2 != null && add2.DistanceSquared < SpellMaxRangeSq("Fear")) {
                    var add = add2;
                    // Fear only real close targets which attack us
                    if (CastPreventDouble(
                            "Fear", () =>
                        ( !add.HasAura("Fear")// its not already feared by other ppl.
                        && !add.HasAura("Howl of Terror")// and its not already feared by howl
                        && Target.DistanceSquared <= 6.5 * 6.5// and its close
                        && add.Target == Me// and its targetting me
                        || add.IsCastingAndInterruptible() )// or its casting and we can interrupt it with fear :D
                        && ( FearDoFear && FearTrackingList.Count(x => add.Equals(x.ExpiringObject)) == 0 ) // and its not banned from fear
                        , add, 1000
                        ))
                        return true;
                }

                return false;
            }

            /// <summary>
            /// Determines whether the current pet is a Felguard.
            /// </summary>
            /// <returns><c>true</c> if the current pet is a Felguard; otherwise, <c>false</c>.</returns>
            protected bool HasFelguard()
            {
                return ( WlPetDisplayId.Felguard.Equals(Me.Pet.DisplayId) || WlPetDisplayId.ImpFelguard.Equals(Me.Pet.DisplayId) );
            }

            /// <summary>
            /// Casts the spell on adds.
            /// </summary>
            /// <returns><c>true</c>, if spell on adds was cast, <c>false</c> otherwise.</returns>
            /// <param name="spellName">Spell name.</param>
            protected bool CastSpellOnAdds(string spellName)
            {
                return CastSpellOnAdds(spellName, null);
            }

            /// <summary>
            /// Cast the spell on adds.
            /// </summary>
            /// <returns><c>true</c>, if spell on adds was cast, <c>false</c> otherwise.</returns>
            /// <param name="spellName">Spell name.</param>
            /// <param name="castCondition">Condition which gets a UnitObject to decide if the spell may get cast on it.</param>
            protected bool CastSpellOnAdds(string spellName, Func<UnitObject, bool> castCondition)
            {
                castCondition = castCondition ?? ( add => true );

                foreach (var add in Adds) {
                    if (Cast(
                            spellName,
                            () => castCondition(add),
                            add
                        ))
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Casts the spell on adds. Will prevent multiple casts.
            /// </summary>
            /// <returns><c>true</c>, if spell prevent double on adds was cast, <c>false</c> otherwise.</returns>
            /// <param name="spellName">Spell name to cast.</param>
            /// <param name="castCondition">Condition which gets a UnitObject to decide if the spell may get cast on it.</param>
            protected bool CastSpellPreventDoubleOnAdds(string spellName, Func<UnitObject, bool> castCondition)
            {
                castCondition = castCondition ?? ( add => true );

                foreach (var add in Adds) {
                    if (castCondition != null && CastPreventDouble(
                            spellName,
                            () => castCondition(add),
                            add
                        ))
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Will be called when the bot is OutOfCombat.
            /// </summary>
            /// <returns><c>true</c>, if the function should be called again, <c>false</c> otherwise.</returns>
            public override bool OutOfCombat()
            {
                if (HasSpell("Fire and Brimstone") && CastSelf(
                        "Fire and Brimstone",
                        () => Me.HasAura("Fire and Brimstone")
                    ) && HasGlobalCooldown())
                    return true;
                if (CastSelf("Dark Intent", () => !Me.HasAura("Dark Intent")))
                    return true;
                if (CastSelf("Unending Breath", () => Me.IsSwimming && !Me.HasAura("Unending Breath")))
                    return true;
                if (CastSelf("Soulstone", () => CurrentBotName != "Combat" && !Me.HasAura("Soulstone")))
                    return true;

                if (HasSpell("Grimoire of Sacrifice") && !Me.HasAura("Grimoire of Sacrifice")) {
                    if (this.SummonPet(SelectedPet))
                        return true;
                    if (CastSelf("Grimoire of Sacrifice", () => Me.HasAlivePet))
                        return true;
                } else if (UsePet && !API.DisableCombat) {
                    if (HasSpell("Flames of Xoroth") && CastSelf(
                            "Flames of Xoroth",
                            () => !Me.HasAlivePet && Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers) >= 1
                        ))
                        return true;
                    if (this.SummonPet(SelectedPet))
                        return true;
                } else if (Me.HasAlivePet) {
                    Me.PetDismiss();
                }

                return CastSelfPreventDouble("Create Healthstone", () => Inventory.Healthstone == null, 10000);
            }

            /// <summary>
            /// Will be called when leaving combat
            /// </summary>
            /// <returns><c>true</c>, if the function should be called again, <c>false</c> otherwise.</returns>
            public override bool AfterCombat()
            {
                if (HasSpell("Fire and Brimstone") && CastSelf(
                        "Fire and Brimstone",
                        () => Me.HasAura("Fire and Brimstone")
                    ))
                    return true;
                if (HasSpell("Metamorphosis") && CastSelf("Metamorphosis", () => Me.HasAura("Metamorphosis")))
                    return true;
                return false;
            }

            /// <summary>
            /// Do the stuff which got no GCD-
            /// </summary>
            /// <returns><c>true</c>, if there was an GCD after a cast, <c>false</c> otherwise.</returns>
            protected bool DoGlobalStuff()
            {
                //no globalcd
                if (HasSpell("Dark Soul: Instability"))
                    CastSelfPreventDouble(
                        "Dark Soul: Instability",
                        () => Target.IsInCombatRangeAndLoS,
                        120000
                    );
                if (HasSpell("Dark Soul: Knowledge"))
                    CastSelfPreventDouble(
                        "Dark Soul: Knowledge",
                        () => Target.IsInCombatRangeAndLoS,
                        120000
                    );
                if (HasSpell("Dark Soul: Misery"))
                    CastSelfPreventDouble(
                        "Dark Soul: Misery",
                        () => Target.IsInCombatRangeAndLoS,
                        120000
                    );

                Cast(
                    "Command Demon",
                    () => ( this.IsPetActive("Summon Felhunter") || SelectedPet == WarlockPet.Felhunter ) && Target.IsCastingAndInterruptible()
                );
                Cast(
                    "Command Demon",
                    () => !HasSpell("Grimoire of Sacrifice") && ( this.IsPetActive("Summon Imp") || SelectedPet == WarlockPet.SoulImp ) && Me.HealthFraction <= 0.75
                );
                Cast(
                    "Command Demon",
                    () => ( this.IsPetActive("Summon Voidwalker") || SelectedPet == WarlockPet.Voidwalker ) && Me.HealthFraction < 0.5
                );
                Cast("Axe Toss", () => HasFelguard() && Target.IsCastingAndInterruptible()); // Should have no gc as its a pet ability

                //Heal
                if (HasSpell("Unbound Will"))
                    CastSelf("Unbound Will", () => !Me.CanParticipateInCombat); //no gc
                if (HasSpell("Dark Bargain"))
                    CastSelf("Dark Bargain", () => Me.HealthFraction < 0.5); //no gc
                if (HasSpell("Sacrificial Pact"))
                    CastSelf("Sacrificial Pact", () => Me.HealthFraction < 0.6); //no gc
                if (HasSpell("Unending Resolve"))
                    CastSelf("Unending Resolve", () => Me.HealthFraction <= 0.5); //no gc

                if (Me.HasAlivePet) {
                    UnitObject add = Adds.FirstOrDefault(x => x.Target == Me);
                    if (add != null)
                        Me.PetAttack(add);
                }
                    
                return HasGlobalCooldown();
            }

            /// <summary>
            /// This function will keep your pet alive, do some CD DPS and heal yourself if needed and possible.
            /// </summary>
            /// <returns><c>true</c>, if a GCD spell as cast, <c>false</c> otherwise.</returns>
            protected bool DoSomePetAndHealingStuff()
            {
                if (HasSpell("Mortal Coil") && Cast("Mortal Coil", () => Me.HealthFraction <= 0.5))
                    return true;
                if (HasSpell("Dark Regeneration") && CastSelf("Dark Regeneration", () => Me.HealthFraction <= 0.6))
                    return true;
                if (HasSpell("Flames of Xoroth") && CastSelf(
                        "Flames of Xoroth",
                        () => !HasSpell("Grimoire of Sacrifice") && !Me.HasAlivePet && Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers) >= 1
                    ))
                    return true;
                if (UseAdditionalDPSPet && HasSpell("Summon Terrorguard") && Cast(
                        "Summon Terrorguard",
                        () => Me.HpLessThanOrElite(0.5)
                    ))
                    return true;
                if (UseAdditionalDPSPet && HasSpell("Summon Doomguard") && Cast(
                        "Summon Doomguard",
                        () => Me.HpLessThanOrElite(0.5)
                    ))
                    return true;
                if (UseAdditionalDPSPet && HasSpell("Summon Infernal") && Cast(
                        "Summon Infernal",
                        () => Me.HpLessThanOrElite(0.5) || Adds.Count >= 4
                    ))
                    return true;
                if (UseAdditionalDPSPet && HasSpell("Summon Abyssal") && Cast(
                        "Summon Abyssal",
                        () => Me.HpLessThanOrElite(0.5) || Adds.Count >= 4
                    ))
                    return true;
                if (HasSpell("Demonic Rebirth") && CastSelf(
                        "Demonic Rebirth",
                        () => Me.HealthFraction < 0.9 && Target.IsInCombatRangeAndLoS
                    ))
                    return true;
                if (HasSpell("Ember Tap") && CastSelf(
                        "Ember Tap",
                        () => Me.HealthFraction <= 0.35 && Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers) >= 1
                    ))
                    return true;

                return false;
            }
        }
    }
}
