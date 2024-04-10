For creating custom rounds:
 - Forced weapon will be added to player (no preferred) if null, check for preferred
 - Prefered weapon if enabled for primary checks for PrimaryPreference, which is a definition index (see below)
 - Prefered weapon if enabled for secondary checks for Pistol preference, which is a definition index
 - If teamsize is set to more than 1, the plugin check the map if capable to spawn more than 1 player per team, if yes, create teams dynamically if it can match the teamsize

 Possible PrimaryPreference values:
 	Rifle - 0
	Sniper - 1
	SMG - 2
	LMG - 3
	Shotgun - 4
	Pistol - 5
	All Primary - 6