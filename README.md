# Randomly Generated Items Rewrite

This is a rewrite of the [Randomly Generated Items](https://thunderstore.io/package/HIFUPulse/RandomlyGeneratedItems/) mod by HIFU and Pseudopulse. Made with permission.

## Changelog

**2.1.0**
* Add Lunar items! These come with some uniquely powerful effects that do not appear on other items, but also come with negative effects ranging from just a nuisance to run-ending.
	* Be careful to read what randomly generated lunar items do before picking them up! **_It is possible for items to generate with effects that will just instantly kill you_**.
* Heavily optimize the random item generation process, making it about 50x faster. As part of this, random sprite generation is now asyncronous and lazily performed - this means that if you generate a very large number of items, some of them may not have their sprites generated yet when the loading screen finishes. They will continue to generate until they finish, and this may cause a noticable drop in framerate for a while until it is done.
* Various significant changes to the internal structure of the mod. Many of these are in preparation for adding an option to regenerate items without restarting the game in the future, although that feature is not finished yet.
* Bug fixes, including fixing boss items drops and the item selection from the Halcyon Shrine.
* Add some new restrictive conditions. This does not effect how likely it is for items to have conditions on them, just increases the variety of what the conditions can be.
* Nerf the amount of jumps given by 'gain x maximum jump count' items.

**2.0.7**
* Hotfix for issue that would freeze the game on the loading screen if you toggled off every single passive and triggered effect, but not every trigger type.

**2.0.6**
* The config has been massively expanded with numerous toggles that allow you to control what can appear on randomly generated items.
* Fix buffs given by randomly generated items not having icons.
	* The 'no max barrier' buff is a yellow circle with a black border, and the 'bypass all restrictive conditions' buff is a cyan diamond with a black border.
	* Buffs given by items that temporarily boost stats when triggered have the same icon as the item that grants the buff.
* Fix randomly generated item pickups having solid collision.
* Fix equipment items with passive effects on them having shorter cooldowns than ones that do not have passive effects. They now have longer cooldowns, instead of shorter ones.
* Added boosts to jump height and max jumps to the pool of passive random effects.
* Buffed the 'bypass all restrictive conditions' equipment effect heavily by doubling its duration and halving its cooldown.
* Nerfed the amount of critical chance given by random items, so that it will be a bit harder to reach the point of being pinned at 100% critical chance.
* Nerfed the 'immediately activate all triggered effects' equipment effect so that effects triggered by the equipment will be weaker than they normally would be.
* Restore 'lore.txt' from the original Randomly Generated Items mod that was accidentally removed, which makes the item descriptions in the logbook marginally more interesting.

**2.0.5**
* Fix the "while midair" condition, which previously just did not work at all.
* Fix the "reduce equipment cooldown" effect, which previously reduced special skill cooldown instead.
* Fix the Halcyon Shrine giving no rewards when the Artifact of Frivolity is active. At least, I hope it is fixed, I don't own the Seekers of the Storm DLC and cannot test it...
* Adjusted both initial strength and stacking of effects on green, red, and void items.
* Add text to void item descriptions indicating the item that they corrupt.
* Random boss items can now be generated, although there is nothing special about them. They will be expanded upon with some unique effects in a future update.
* Enemies that drop specific items no longer bypass the Artifact of Frivolity. Instead, they will drop one specific randomly generated item of the same tier.

**2.0.4**

* Add a handful of special effects that only appear on equipment, never on normal items.
* Fix the Chain Lightning effect
* Fix the "Upon getting healed, receive healing..." effect combination crashing the game.
* Fix barrier nerf from 2.0.3 not actually working

**2.0.3**

* Fix armor items only giving 1% of their listed effect
* Blacklist the combination of "While not moving..." and "...gain movement speed"
* Attempt to nerf barrier by making it decay increasingly fast when above the cap

**2.0.2**

* Fixed many item effects, including most passive item effects, not working for non-host players in multiplayer
* Fixed many triggered explosions and projectiles dealing far less damage than they should

**2.0.1**

* Removed Noop (Don't worry about it)

**2.0.0**

* Release of Randomly Generated Items Rewrite.
* Rewrite most of the code with a new easily-expandable system.
* Fix a large number of bugs.
* Add Void and Equipment random items.
* The number of items that generate of each tier is now seperately configurable.
* Significantly increase variety of possible random effects.
* Reduce how often restrictive conditions appear on items to reduce how many items are effectively useless.
* When restrictive conditions do appear on items, the items' effects will be strengthed to compensate for the limitations.
* Reduce how often 'gives the player barrier' appears as an effect.
* Attempt to make items more balanced - remains to be seen how effective that is.
* Item sprites and models now are generated with colors and appearances that reflect what the item actually does.
* Items are now visible in the logbook from the beginning, so you can see what items have been generated if you wish, and you can use the Inspect Item mechanic on new random items you find.
* Items are now tied to an expansion that you can enable or disable on the character select screen.
* Adds an artifact called the 'Artifact of Frivolity' that, if enabled, disables all non-random items from generating. Note that this will cause all sources of Lunar items to give nothing, since Lunar items are not (yet) generated by the mod.

**1.0.0**

* Release of original Randomly Generated Items mod.
