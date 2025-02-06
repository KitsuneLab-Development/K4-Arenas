-- 2025.02.06 - 2.0.1

- feat: Introduced configurable default round type for scenarios where players have no matching round preferences
- fix: Resolved queue assignment issues when warmup_enabled convars was disabled

-- 2025.02.04 - 2.0.0

- feat: Added automated challenge systems for bots
- feat: Enhanced challenge notifications in screen center
- feat: Added warmup status indicators
- feat: Implemented bot arena match handling
- feat: Added support for CYBERSHOKE maps
- upgrade: New smart arena detection system
- upgrade: Rebuilt duel mechanics from ground up
- optimise: Reduced round end processing overhead
- optimise: Better warmup player handling
- optimise: Smarter warmup population timing
- optimise: Code cleanup for better performance
- fix: Map transition round timing issues
- fix: Player preferences persistence
- fix: Challenge position assignments
- fix: Challenge end state handling
- fix: Spawn conflicts with zero warmup
- fix: Random team switch problems
- fix: Multiple arena allocation fixes

-- 2025.01.28 - 1.5.5

- feat: Added detection for team changes from Anti-AFK and third-party plugins to handle arena AFK state
- feat: Implemented default "Preferred" SSLMode fallback for invalid database configurations
- feat: Player-specific translations now respect individual language settings (changeable via !lang command)
- feat: Introduced HTML-based round announcements in screen center during freeze time
- upgrade: Refactored menu system for improved preference handling and display
- optimise: Streamlined codebase with simplified logic patterns
- remove: Discontinued support for MetaMod skinchanger compatibility

-- 2024.12.20 - 1.5.4

- fix: PerformAFKAction didnt change the AFK state of player
- fix: Ghost respawning when using AFK command in the first one second after spawn

-- 2024.12.09 - 1.5.3

- feat: Introduced a new menu API
- fix: Removed unecessary : symbols in guns menu
- fix: Selecting random in guns menu didn't update the menu properly
- fix: Added missing : symbol to Random selections

-- 2024.12.?? - 1.5.2

??? What happened here

-- 2024.10.24 - 1.5.1 - @originalaidn

- fix: Rifle option in gun menu
- fix: Multiple tick marks in guns pref menu

-- 2024.10.08 - 1.5.0

- feat: Added CenterMenu support using KitsuneMenu
- feat: Added API to get current arena id for a player
- feat: Added API to get arena name for a player (including WARMUP, CHALLANGE, WAITING, ARENA1, etc (for Zenith support))
- feat: Added PerformAFKAction to API to allow AntiAFK plugins to support Arenas (see examples)
- feat: Added option to disable clantag changes (better Zenith compatibility)
- upgrade: CSS api compile version
- fix: Fix round terminate issues (thanks for @letaryat)
- fix: Breaking scoreboard with using 'mp_halftime 0' in the predefined configs

-- 2024.09.17 - 1.4.6

- fix: Cancel challanges if opponent gets to AFK to prevent exploits (Thanks to Siudzix)

-- 2024.09.17 - 1.4.5

- feat: MVPs adding only when playing in arena 1
- upgrade: Round termination algorithm
- optimise: Optimised the script here and there
- fix: FlashXMLHintFix incompatibility

-- 2024.06.02 - 1.4.4

- feat: Experimental setting for arena math override (for wierd maps)
- fix: Team assignment glitches
- fix: Warmup stuck glitches
- fix: Warmup not always respawn people

-- 2024.05.11 - 1.4.3

- feat: Added log and stop to find arena if no spawns on the map at all (crash causer)

-- 2024.05.09 - 1.4.2

- feat: New setting to disable knife giving by default (#14)
- fix: Compile warning after CSS update
- fix: Arena scoreboard order damage support, not just score

-- 2024.04.26 - 1.4.1

- feat: Add support for the plugin to work without database (disables preferences, experimental)
- feat: Block damage from other arena players (useful for HvH, new ConVar)
- feat: Block flashes from other arena players (useful for HvH, new ConVar)
- fix: Unknown weapon type listed in preferences and throws error
- fix: Challenges not ended properly

-- 2024.04.26 - 1.3.10

- fix: Team switch problems after mapchange

-- 2024.04.26 - 1.3.9

- feat: Selecting SPEC/Teams toggles AFK mode automatically
- fix: Auto team select being blocked
- fix: Networking logs
- fix: Using !afk to get out of AFK mode didnt change back the clantag
- fix: Respect round restart delay (toorisrael)
- fix: Duplicated round terminates after round ends

-- 2024.04.21 - 1.3.8

- fix: Challenge not ended properly
- fix: Challenging yourself
- fix: Arena name in chat if it's challange/warmup
- fix: Challenge players not removed from normal queue
- fix: Preferences lost on map change

-- 2024.04.17 - 1.3.7

- feat: Add pre-defined game configs (by Mesharsky)
- feat: Add Polish translation (by Mesharsky)
- refactor: Remove some redundant code
- fix: Arena finding algorithm now smart remove and repair

-- 2024.04.15 - 1.3.6

- feat: New option to disable round types befault in player preferences
- feat: Added clantag force to always enforce arena clantags
- fix: Changed default weapons to use classnames instead of indexes
- fix: No opponent round now rolls first player's roundtypes instead of random
- fix: Last roundtype cannot be disabled to have atleast one selected

-- 2024.04.14 - 1.3.5

- refactor: AFK and Command announcements are now sent only on player join, not every round
- refactor: Change settings to use weapon names instead of IDs (easier for users)
- perf: Map changes wont reload all players anymore
- perf: Preferences wont be saved instantly to MySQL, but on disconnect
- fix: Warmup weapon allocation problems
- fix: Duplicated arena loading on map change
- fix: Preferences not get loaded on hotreload
- fix: State change related problems
- fix: Better CSS skinchanger knife support
- fix: AFK reminder message is now sent only once
- fix: Bots being processed in MySQL
- fix: MySQL insert query problems

-- 2024.04.13 - 1.3.4

- fix: Possible fix for GiveNamedItem2 problems on some servers
- fix: The default weapons are now customizable

-- 2024.04.11 - 1.3.3

- fix: Remove HLTV from queues(?)

-- 2024.04.11 - 1.3.2

- fix: Challenge cmd, not in arena problem
- fix: Armor/Helmet problems
- fix: Weapons not given (convar problem)

-- 2024.04.11 - 1.3.1

- feat: Add support for Windows MetamodSkinChanger support
- feat: Worst scenario filter for queue to prevent duplicates
- fix: Add missing text, if no command found for chat message

-- 2024.04.09 - 1.3.0

- feat: Add challenges (duels)
- feat: Public developer API for round types
- feat: Example dodgeball round type
- fix: Helmet and armor give/remove problems
- fix: Loading delay (now the plugin load instantly)
- optimise: Weapon giving logics
- optimise: Plugin loading mechanism

-- 2024.04.06 - 1.2.1

- feat: Windows support added back
- fix: HardwareID is changing with docker reboot
- fix: Health getter throws null pointer exception
- fix: Arena clantag not changes if player stay alive
- upgrade: Hardware lock algorithm

-- 2024.04.04 - 1.2.0

- upgrade: Arena setup algorithm
- upgrade: Arena models
- upgrade: Warmup weapon allocation

-- 2024.04.04 - 1.1.3

- feat: Order scoreboard by ArenaID
- fix: Warmup fixed maximally with upgrades
- fix: VirusTotal false alarm
- fix: GameRules causing errors
- fix: Block checking for team based rountype if there are none (performance)
- fix: Remove round types from MySQL that not exists on server anymore

-- 2024.04.03 - 1.1.2

- feat: Block manual team changes
- upgrade: Warmup arenas now spawn in solo mode
- fix: RoundType numbers not matching
- fix: Warmup every CS2 warmup problem
- fix: Wrong item spawn problems
- fix: Round preferences not being saved
- optimise: Warmup arena logic
- optimise: Round Terminator logic
- optimise: Arena end logic

-- 2024.04.03 - 1.1.1

- feat: Add support for warmup arenas
- fix: Queue stuck problems
- fix: Crash on map change
- fix: Spawn location removed from the list after being used
- fix: Weapon preference not found error
- fix: Round terminator result
- fix: Arena player assignment logic
- upgrade: Replaced custom database logic with ORM
- upgrade: Round terminator logic
- upgrade: Player death handler logic
- upgrade: Arena weapon setup logic
- upgrade: Arena location allocation logic
- upgrade: Arena setup player logic
- upgrade: Arena result logic
- upgrade: Arena player assignment logic
- optimise: GetArenaPlayer
- optimise: GetCommonRoundType
- optimise: Random generators
- optimise: AddTeamsToArena
- optimise: ShowWeaponPreferenceMenu
- optimise: GetOpponentNames
- optimise: Full WeaponModel
- remove: Windows support

-- 2024.04.01 - 1.1.0

- feat: Add support for 2v2,3v3 and any x vs x modes
- feat: Add configurable round types to config
- upgrade: Arena restart logic
- upgrade: Arena spawn finder logic
- upgrade: Arena allocator logic
- upgrade: Arena player assignment logic
- upgrade: Round terminator logic
- optimise: Full arena and player models

-- 2024.04.01 - 1.0.3

- feat: Add skin compatibility for metamod skinchangers
- feat: Add "Random" weapon selection to weapon preferences
- fix: Restart arena on second player join to fasten up the process
- fix: Problems caused by default weapons
- fix: Arena clantag missmatches
- fix: T spawn remained log bug
- fix: Round terminated when it shouldn't
- remove: Round preference debug logs
- optimise: Round winner and result choosing logic
- optimise: Arena setup logic

-- 2024.03.31 - 1.0.1

- feat: Added AFK mode
- feat: Added custom command lists to settings
- feat: Waiting and AFK labels in the scoreboard
- fix: Plugin not loading with hibernate enabled
- fix: Database connection count problems
- fix: Some players not assigned to arenas
