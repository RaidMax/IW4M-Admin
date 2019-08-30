# IW4MAdmin Anticheat
**Initial document draft | 8.30.19**

**IW4MAdmin** anticheat for IW4x uses data from in-game logs to track player locations, hit locations, and view angles
to validate against a known set of play styles.
Every hit event ocurring in the game (Damage or Kill from a gun) is captured by IW4MAdmin and analyzed.
**Session analysis** occurs against all hit events since the player connected to the server.
**Lifetime analysis** occurs against all hit events ever occuring for a given player.
## Detection Types
### Bone
Compares the number of times a particular bone is hit against the number of hits on all other bones.
Many rudimentary aimbots lock onto a particular player bone position such as _Head_, or _Upper Torso_.
This detection method has the highest chance of a false positive, as non-cheaters can play abnormally 
(e.g. going for headshots, or using a weapon that unconciously changes their playstyle)

#### Hit Location Reference
| Number | Hit Location    |
|--------|-----------------|
| 2      | Head            |
| 3      | Neck            |
| 4      | Upper Torso     |
| 5      | Lower Torso     |
| 6      | Upper Right Arm |
| 7      | Upper Left Arm  |
| 8      | Lower Right Arm |
| 9      | Lower Left Arm  |
| 10     | Right Hand      |
| 11     | Left Hand       |
| 12     | Upper Right Leg |
| 13     | Upper Left Leg  |
| 14     | Lower Right Leg |
| 15     | Right Foot      |
| 16     | Left Foot       |
### Chest
Identical to *Bone* detection, except it specifically compares the ratio of Upper Torso / Lower Torso hit counts.
It can be thought of as a focused bone detection type, as most aimbots don't aim to unusual bones such as a foot.
As with *Bone* detection, this is prone to false positives.
### Offset
Compares the player angles from three snapshots. The first snapshot being the snapshot immediately before the server registered the hit for a player.
The second snapshot is the "frame" the server registered the kill. The final snapshot is the snapshot immediately after the server registers the hit.
The algorithm is:
```
let a = first snapshot angles
let b = second snapshot angles
let c = third snapshot angles
offset = ((a - b) + (c - b)) - (a-c)
```
This detection method is very effective at detecting silent aimbots. 
Silent aimbots "fake" a shot by changing a player's view angles for a single snapshot. From a spectator's position, the single snapshot view angle altering is not visible. 
The larger "FOV" (field of view) from a silent aimbot, the more absolute difference between the three snapshots. 
Over time if the average distance between these two viewangles is higher than expected, a detection is triggered.
False positives are very rare with this detection. However, extreme client lag can trigger a false positive in special situations.
### Strain
Analyzes the frequency, viewangle distance, and player distance between hits.
The algorithm is:
```
let v = view angle distance between two hits  
let t = delta time (time between hits)  
let d = distance between the attacker and victim
let s = decay over time
strain = ((v / t) * d)^s
```
A high value indicates fast target switching at large distance intervals, which if done naturally, requires an extreme level of mechanical effort.
"Rage" aimbots commonly switch to targets as fast as possible in any direction.
### Recoil
Compares the average of the last few snapshots of player angles to 0. Client sided no recoil prevents the view angle "Z" axis from changing.
If the average view angle "Z" axis value remains 0, the detection is triggered. 
As of 8.30.19, there are no known false positives for this detection.
Several weapons which do not have any recoil from the game are excluded in this detection.
### Format
Detection penalty reasons are as follow:
<_detectionType_>-<_location/value_>@<_hitCount_>
Example:
- detectionType = Strain
- value = 1.39
- hitCount = 136

Result: **Strain-1.39@136**
This reason is only visible to logged in privileged users.
