
### Server reference PerformanceBucket

EFPerformanceBucket adding as DbSet to DatabaseContext

Create migration for EFClientStat


### Leaderboard logic

Using individual stats - but groups them by the bucket

Determines what the performance is by all the stats in that bucket / per server and for global stats. (BUCKET, SERVER, GLOBAL)
- Isn't properly calculating, when it goes to try and find where a person ranked in a performance bucket, it was returning null(?) so they never got a rank
- Potentially resolved by using EFPerformanceBucketId instead of arbitrary string


If new buckets are added to config, check on start and add to DB (PerformanceBucketConfgiuration)
- Needs to update to use the code
- CODE LOC `EnsureServerAdded()`

LOG OnPlayerRoundDataGameEvent ZOMBIES `CID - 848637`

### 2024-09-05
- Fixed references
- Added DbSet
- MIGRATION NOT CREATED
- StatManager Line 1313 needs checking.

