package com.ashersoft.kakaobot.data

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query

@Dao
interface SlotV2RunDao {
    @Query("SELECT * FROM slot_v2_run WHERE linkId = :linkId AND ownerKey = :ownerKey")
    suspend fun find(linkId: Long, ownerKey: String): SlotV2RunRow?

    @Query("SELECT * FROM slot_v2_run WHERE linkId = :linkId AND ownerUserId = :userId AND ownerUserId > 0 LIMIT 1")
    suspend fun findByUserId(linkId: Long, userId: Long): SlotV2RunRow?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(row: SlotV2RunRow)

    @Query("DELETE FROM slot_v2_run WHERE linkId = :linkId AND ownerKey = :ownerKey")
    suspend fun delete(linkId: Long, ownerKey: String)

    @Query("DELETE FROM slot_v2_run WHERE lastActionAt < :before")
    suspend fun purgeExpired(before: Long)
}

@Dao
interface SlotV2AchDao {
    @Query("SELECT * FROM slot_v2_ach WHERE linkId = :linkId AND ownerKey = :ownerKey")
    suspend fun find(linkId: Long, ownerKey: String): SlotV2AchRow?

    @Query("SELECT * FROM slot_v2_ach WHERE linkId = :linkId AND userId = :userId LIMIT 1")
    suspend fun findByUserId(linkId: Long, userId: Long): SlotV2AchRow?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(row: SlotV2AchRow)
}

@Dao
interface SlotV2ScoreDao {
    @Query("SELECT * FROM slot_v2_score WHERE linkId = :linkId AND nickname = :nickname")
    suspend fun find(linkId: Long, nickname: String): SlotV2ScoreRow?

    @Query("SELECT * FROM slot_v2_score WHERE linkId = :linkId AND userId = :userId LIMIT 1")
    suspend fun findByUserId(linkId: Long, userId: Long): SlotV2ScoreRow?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(row: SlotV2ScoreRow)

    @Query("SELECT * FROM slot_v2_score WHERE linkId = :linkId")
    suspend fun allForLink(linkId: Long): List<SlotV2ScoreRow>

    @Query("""
        SELECT linkId, MAX(nickname) AS nickname, MAX(bestScore) AS bestScore,
               SUM(totalScore) AS totalScore, SUM(runs) AS runs, MAX(bestStage) AS bestStage,
               MAX(bestChar) AS bestChar, MAX(bestMachine) AS bestMachine,
               MAX(lastPlayedAt) AS lastPlayedAt, userId, MAX(ownedDevices) AS ownedDevices,
               MAX(pinnedChallenge) AS pinnedChallenge, MAX(lastCombo) AS lastCombo
          FROM slot_v2_score
         WHERE linkId = :linkId AND userId IS NOT NULL
         GROUP BY linkId, userId
         UNION ALL
        SELECT linkId, nickname, bestScore, totalScore, runs, bestStage,
               bestChar, bestMachine, lastPlayedAt, userId, ownedDevices, pinnedChallenge, lastCombo
          FROM slot_v2_score WHERE linkId = :linkId AND userId IS NULL
         ORDER BY bestScore DESC LIMIT :limit
    """)
    suspend fun topByBest(linkId: Long, limit: Int): List<SlotV2ScoreRow>
}
