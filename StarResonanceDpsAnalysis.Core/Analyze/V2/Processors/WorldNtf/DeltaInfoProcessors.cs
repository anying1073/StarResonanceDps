using Google.Protobuf;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.BlueProto;
using StarResonanceDpsAnalysis.Core.Extends.System;
using StarResonanceDpsAnalysis.Core.Logging;
using StarResonanceDpsAnalysis.Core.Tools;
using Zproto;

namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;

/// <summary>
/// Processes delta info messages for damage and healing events.
/// </summary>
public abstract class BaseDeltaInfoProcessor(IDataStorage storage, ILogger? logger, WorldNtfMessageId id) : WorldNtfBaseProcessor(id)
{
    protected readonly IDataStorage _storage = storage;
    protected readonly ILogger? _logger = logger;

    protected void ProcessAoiSyncDelta(Zproto.WorldNtf.Types.AoiSyncDelta? delta)
    {
        if (delta == null) return;

        var targetUuidRaw = delta.Uuid;
        if (targetUuidRaw == 0) return;

        var isTargetPlayer = targetUuidRaw.IsUuidPlayerRaw();
        var targetUuid = targetUuidRaw.ShiftRight16();

        var attrCollection = delta.Attrs;
        if (attrCollection?.Attrs != null && isTargetPlayer)
        {
            _storage.EnsurePlayer(targetUuid);

            foreach (var attr in attrCollection.Attrs)
            {
                if (attr.Id == 0 || attr.RawData == null || attr.RawData.Length == 0) continue;
                var reader = new CodedInputStream(attr.RawData.ToByteArray());

                var attrType = (EAttrType)attr.Id;
                switch (attrType)
                {
                    case EAttrType.AttrName:
                        _storage.SetPlayerName(targetUuid, reader.ReadString());
                        break;
                    case EAttrType.AttrProfessionId:
                        _storage.SetPlayerProfessionID(targetUuid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrFightPoint:
                        _storage.SetPlayerCombatPower(targetUuid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrLevel:
                        _storage.SetPlayerLevel(targetUuid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrRankLevel:
                        _storage.SetPlayerRankLevel(targetUuid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrCri:
                        _storage.SetPlayerCritical(targetUuid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrLuck:
                        _storage.SetPlayerLucky(targetUuid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrHp:
                        _storage.SetPlayerHP(targetUuid, reader.ReadInt32());
                        break;

                    case EAttrType.AttrSeasonStrength: 
                    case EAttrType.AttrSeasonStrengthTotal:
                    case EAttrType.AttrSeasonStrengthAdd:
                    case EAttrType.AttrSeasonStrengthExAdd:
                    case EAttrType.AttrSeasonStrengthPer:
                    case EAttrType.AttrSeasonStrengthExPer:
                        //_logger?.LogWarning("[BaseDeltaInfoProcessor] Test for get AttrDreamIntensity: targetUuid[{targetUuid}], intensity[{value}]", targetUuid, reader.ReadInt32());
                        var strength = reader.ReadInt32();
                        _storage.SetPlayerSeasonStrength(targetUuid, strength);
                        break;
                    case EAttrType.AttrMaxHp:
                        _storage.SetPlayerMaxHP(targetUuid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrCombatState:
                        _storage.SetPlayerCombatState(targetUuid, reader.ReadBool());
                        break;
                    case EAttrType.AttrCombatStateTime:
                        _storage.SetPlayerCombatStateTime(targetUuid, reader.ReadInt32());
                        break;
                    /*
                case EAttrType.AttrElementFlag:
                    _storage.SetPlayerElementFlag(targetUuid, reader.ReadInt32());
                    break;
                case EAttrType.AttrReductionLevel:
                    _storage.SetPlayerReductionLevel(targetUuid, reader.ReadInt32());
                    break;
                case EAttrType.AttrEnergyFlag:
                    _storage.SetPlayerEnergyFlag(targetUuid, reader.ReadInt32());
                    break;
                case EAttrType.AttrReduntionId:
                    */
                    case EAttrType.AttrId:
                        _ = reader.ReadInt32();
                        break;
                    case EAttrType.AttrUnknown:
                    case EAttrType.AttrAimDir:
                    case EAttrType.AttrScale:
                    case EAttrType.AttrScaleAddRatio:
                    case EAttrType.AttrState:
                    case EAttrType.AttrCamp:
                    case EAttrType.AttrLayer:
                    case EAttrType.AttrIsBodySeparate:
                    case EAttrType.AttrConfigUid:
                    case EAttrType.AttrTableUid:
                    case EAttrType.AttrVisualLayers:
                    case EAttrType.AttrVisualLayer:
                    case EAttrType.AttrVisualLayerUid:
                    case EAttrType.AttrSummonFlag:
                    case EAttrType.AttrTargetId:
                    case EAttrType.AttrTargetPartId:
                    case EAttrType.AttrIsBot:
                    case EAttrType.AttrBasicFleshyType:
                    case EAttrType.AttrDir:
                    case EAttrType.AttrTargetDir:
                    case EAttrType.AttrPos:
                    case EAttrType.AttrTargetPos:
                    case EAttrType.AttrSummonerPos:
                    case EAttrType.AttrVelocity:
                    case EAttrType.AttrMoveType:
                    case EAttrType.AttrTurnVelocity:
                    case EAttrType.AttrReviveCurProgressValue:
                    case EAttrType.AttrReviveMaxProgressValue:
                    case EAttrType.AttrUnbreakableLevel:
                    case EAttrType.AttrStateTime:
                    case EAttrType.AttrDeadType:
                    case EAttrType.AttrMoveForce:
                    case EAttrType.AttrSummonerId:
                    case EAttrType.AttrTopSummonerId:
                    case EAttrType.AttrIsUnderGround:
                    case EAttrType.AttrOffset:
                    case EAttrType.AttrInheritingType:
                    case EAttrType.AttrFightSourceInfo:
                    case EAttrType.AttrSkillId:
                    case EAttrType.AttrSkillStage:
                    case EAttrType.AttrInAccumulate:
                    case EAttrType.AttrSkillLevel:
                    case EAttrType.AttrSkillStageBeginTime:
                    case EAttrType.AttrSkillBeginTime:
                    case EAttrType.AttrSkillSpeed:
                    case EAttrType.AttrSkillStageNum:
                    case EAttrType.AttrReplaceSkillList:
                    case EAttrType.AttrFinalTargetDir:
                    case EAttrType.AttrSkillUuid:
                    case EAttrType.AttrIsCurStageNeedStopMove:
                    case EAttrType.AttrSkillShowState:
                    case EAttrType.AttrSwitchSkill:
                    case EAttrType.AttrSkillLevelIdList:
                    case EAttrType.AttrCantSilence:
                    case EAttrType.AttrFinalTargetPos:
                    case EAttrType.AttrTargetPartPos:
                    case EAttrType.AttrDmgTargetPos:
                    case EAttrType.AttrSkillRemodelLevel:
                    case EAttrType.AttrIsInteractionActive:
                    case EAttrType.AttrInteractionId:
                    case EAttrType.AttrInteractionUuid:
                    case EAttrType.AttrInteractionStage:
                    case EAttrType.AttrInteractionSeat:
                    case EAttrType.AttrInteractionInfo:
                    case EAttrType.AttrActionTime:
                    case EAttrType.AttrActionId:
                    case EAttrType.AttrActionUpperTime:
                    case EAttrType.AttrActionUpperId:
                    case EAttrType.AttrActionSource:
                    case EAttrType.AttrActionLongName:
                    case EAttrType.AttrMountId:
                    case EAttrType.AttrMountSize:
                    case EAttrType.AttrActionGroupInfo:
                    case EAttrType.AttrAiming:
                    case EAttrType.AttrGender:
                    case EAttrType.AttrInBattleShow:
                    case EAttrType.AttrFaceData:
                    case EAttrType.AttrProfile:
                    case EAttrType.AttrBodySize:
                    case EAttrType.AttrRoleLevel:
                    case EAttrType.AttrOfflineTime:
                    case EAttrType.AttrClimbType:
                    case EAttrType.AttrClimbNormal:
                    case EAttrType.AttrClimbDir:
                    case EAttrType.AttrClimbTime:
                    case EAttrType.AttrPlaneId:
                    case EAttrType.AttrCanSwitchLayer:
                    case EAttrType.AttrTeamId:
                    case EAttrType.AttrTeamMemberNums:
                    case EAttrType.AttrSeasonLv:
                    case EAttrType.AttrUseItemState:
                    case EAttrType.AttrProfessionSwitchTime:
                    case EAttrType.AttrProfessionHitType:
                    case EAttrType.AttrEquipData:
                    case EAttrType.AttrFashionData:
                    case EAttrType.AttrAppearanceData:
                    case EAttrType.AttrWeaponVisibility:
                    case EAttrType.AttrCommonSkillList:
                    case EAttrType.AttrDeadTime:
                    case EAttrType.AttrResourceLeft:
                    case EAttrType.AttrResourceRight:
                    case EAttrType.AttrShowPieceAttrList:
                    case EAttrType.AttrSceneInteractionInfo:
                    case EAttrType.AttrWeather:
                    case EAttrType.AttrDayNightSwitch:
                    case EAttrType.AttrRankId:
                    case EAttrType.AttrEmoteTime:
                    case EAttrType.AttrEmoteId:
                    case EAttrType.AttrSwitchProfessionCd:
                    case EAttrType.AttrProfessionSkinId:
                    case EAttrType.AttrShowId:
                    case EAttrType.AttrSlot:
                    case EAttrType.AttrShowRankStar:
                    case EAttrType.AttrFishingData:
                    case EAttrType.AttrPersonalTitle:
                    case EAttrType.AttrReviveCount:
                    case EAttrType.AttrSceneAreaId:
                    case EAttrType.AttrSkillSkinIds:
                    case EAttrType.AttrToy:
                    case EAttrType.AttrIsNewbie:
                    case EAttrType.AttrMoveVersion:
                    case EAttrType.AttrPersonalObjData:
                    case EAttrType.AttrTerrainIndex:
                    case EAttrType.AttrIsBackflow:
                    case EAttrType.AttrParkourStep:
                    case EAttrType.AttrParkourFallenJump:
                    case EAttrType.AttrParkourShimmyJump:
                    case EAttrType.AttrParkourFiveJump:
                    case EAttrType.AttrParkourKickWallJump:
                    case EAttrType.AttrParkourPedalWall:
                    case EAttrType.AttrParkourRun:
                    case EAttrType.AttrParkourLazyJump:
                    case EAttrType.AttrParkourLevitation:
                    case EAttrType.AttrShimmyJumpPac:
                    case EAttrType.AttrMaxShimmyJumpPac:
                    case EAttrType.AttrJumpStep:
                    case EAttrType.AttrJumpDir:
                    case EAttrType.AttrVerVelocity:
                    case EAttrType.AttrHorVelocity:
                    case EAttrType.AttrJumpType:
                    case EAttrType.AttrGravity:
                    case EAttrType.AttrBounceJumpId:
                    case EAttrType.AttrJumpExCount:
                    case EAttrType.AttrRushDirection:
                    case EAttrType.AttrBattleRushChargeBegin:
                    case EAttrType.AttrRushMaxCount:
                    case EAttrType.AttrRushCountClearInterval:
                    case EAttrType.AttrRushCd:
                    case EAttrType.AttrGlideVelocityH:
                    case EAttrType.AttrGlideVelocityV:
                    case EAttrType.AttrGlideRotAngle:
                    case EAttrType.AttrWallNormal:
                    case EAttrType.AttrPedalWallDir:
                    case EAttrType.AttrPedalWallStage:
                    case EAttrType.AttrInsightFlag:
                    case EAttrType.AttrAttachVelocity:
                    case EAttrType.AttrAttachVelocityDirX:
                    case EAttrType.AttrAttachVelocityDirY:
                    case EAttrType.AttrAttachVelocityDirZ:
                    case EAttrType.AttrAttachVelocitySource:
                    case EAttrType.AttrAttachSourceEntUuid:
                    case EAttrType.AttrTunnelMoveStage:
                    case EAttrType.AttrTunnelId:
                    case EAttrType.AttrTunnelPointIndex:
                    case EAttrType.AttrTunnelPointT:
                    case EAttrType.AttrSwimStage:
                    case EAttrType.AttrSceneName:
                    case EAttrType.AttrSceneBasicId:
                    case EAttrType.AttrSceneUuid:
                    case EAttrType.AttrSceneChannel:
                    case EAttrType.AttrSceneWeather:
                    case EAttrType.AttrSceneLevelId:
                    case EAttrType.AttrSceneDayNightSwitch:
                    case EAttrType.AttrFireworkStartTimeSeconds:
                    case EAttrType.AttrDeathCount:
                    case EAttrType.AttrDeathSubTimeSecond:
                    case EAttrType.AttrFireworkType:
                    case EAttrType.AttrSceneLineKickUserEndTime:
                    case EAttrType.AttrObjState:
                    case EAttrType.AttrObjCounter:
                    case EAttrType.AttrOwner:
                    case EAttrType.AttrToyState:
                    case EAttrType.AttrDynamicInteractionId:
                    case EAttrType.AttrZoneParam:
                    case EAttrType.AttrDataType:
                    case EAttrType.AttrRotation:
                    case EAttrType.AttrShape:
                    case EAttrType.AttrGmgod:
                    case EAttrType.AttrShapeshiftType:
                    case EAttrType.AttrShapeshiftConfigId:
                    case EAttrType.AttrShapeshiftProfessionId:
                    case EAttrType.AttrShapeshiftSkillIds:
                    case EAttrType.AttrShapeshiftReplaceAttr:
                    case EAttrType.AttrNpcTest:
                    case EAttrType.AttrHostId:
                    case EAttrType.AttrEventId:
                    case EAttrType.AttrEffectType:
                    case EAttrType.AttrBulletTargetPos:
                    case EAttrType.AttrRayCount:
                    case EAttrType.AttrRotate:
                    case EAttrType.AttrSummonGroup:
                    case EAttrType.AttrSummonIndex:
                    case EAttrType.AttrSummonGroupCount:
                    case EAttrType.AttrBulletStage:
                    case EAttrType.AttrBulletCanMove:
                    case EAttrType.AttrBulletCantHit:
                    case EAttrType.AttrBulletZoomType:
                    case EAttrType.AttrBulletOrientationType:
                    case EAttrType.AttrBanDestroyShow:
                    case EAttrType.AttrBulletSpeedChangePct:
                    case EAttrType.AttrDirX:
                    case EAttrType.AttrDirZ:
                    case EAttrType.AttrTargetDirX:
                    case EAttrType.AttrTargetDirZ:
                    case EAttrType.AttrMaxExtinction:
                    case EAttrType.AttrExtinction:
                    case EAttrType.AttrMaxStunned:
                    case EAttrType.AttrStunned:
                    case EAttrType.AttrInOverdrive:
                    case EAttrType.AttrIsLockStunned:
                    case EAttrType.AttrTargetUuid:
                    case EAttrType.AttrAlertIncreaseSpeed:
                    case EAttrType.AttrAlertValue:
                    case EAttrType.AttrStopBreakingBarTickingFlag:
                    case EAttrType.AttrIsStopBehvTree:
                    case EAttrType.AttrBreakingStage:
                    case EAttrType.AttrFirstAttack:
                    case EAttrType.AttrDungeonScoreExtraMultiple:
                    case EAttrType.AttrDungeonScoreExtraAddValue:
                    case EAttrType.AttrIsMonsterRankEnable:
                    case EAttrType.AttrMonsterRank:
                    case EAttrType.SupplementaryRewardIndex:
                    case EAttrType.AttrMonsterSeasonLevel:
                    case EAttrType.AttrHatedCharId:
                    case EAttrType.AttrHatedJob:
                    case EAttrType.AttrHatedName:
                    case EAttrType.AttrHateList:
                    case EAttrType.AttrDropType:
                    case EAttrType.AttrItemId:
                    case EAttrType.AttrAwardId:
                    case EAttrType.AttrAni:
                    case EAttrType.AttrItemData:
                    case EAttrType.AttrInteractionActor:
                    case EAttrType.AttrCollectCounter:
                    case EAttrType.AttrTransferType:
                    case EAttrType.AttrRogueEntryList:
                    case EAttrType.AttrRogueLockedEntry:
                    case EAttrType.AttrStiffType:
                    case EAttrType.AttrStiffStage:
                    case EAttrType.AttrStiffStageTime:
                    case EAttrType.AttrStiffDir:
                    case EAttrType.AttrStiffTime:
                    case EAttrType.AttrStiffDownTime:
                    case EAttrType.AttrStiffHangTime:
                    case EAttrType.AttrStiffTarget:
                    case EAttrType.AttrStiffFlowSpeed:
                    case EAttrType.AttrStiffFlowOffset:
                    case EAttrType.AttrStiffFlowRadius:
                    case EAttrType.AttrStiffHorSpeed:
                    case EAttrType.AttrStiffHorAccSpeed:
                    case EAttrType.AttrStiffVerSpeedUp:
                    case EAttrType.AttrStiffVerAccSpeedUp:
                    case EAttrType.AttrStiffVerSpeedDown:
                    case EAttrType.AttrStiffVerAccSpeedDown:
                    case EAttrType.AttrStiffHorSpeedMinimum:
                    case EAttrType.AttrStiffDamageWeight:
                    case EAttrType.AttrTargetPosIsEnd:
                    case EAttrType.AttrStiffThrowMoveInfo:
                    case EAttrType.AttrStiffDamageStrength:
                    case EAttrType.AttrRideId:
                    case EAttrType.AttrIsCantRide:
                    case EAttrType.AttrRideIndex:
                    case EAttrType.AttrRideStage:
                    case EAttrType.AttrRideType:
                    case EAttrType.AttrRideUuid:
                    case EAttrType.AttrRideAttachEnable:
                    case EAttrType.AttrRideMagneticEnable:
                    case EAttrType.AttrRideMagneticQueueId:
                    case EAttrType.AttrIsForceRide:
                    case EAttrType.AttrControllerUuid:
                    case EAttrType.AttrPassengerList:
                    case EAttrType.AttrIsSilence:
                    case EAttrType.AttrIsConfine:
                    case EAttrType.AttrRideSeatCantTransfer:
                    case EAttrType.AttrCantSwim:
                    case EAttrType.AttrGmcantHit:
                    case EAttrType.AttrCantStiff:
                    case EAttrType.AttrCantStiffBack:
                    case EAttrType.AttrCantStiffDown:
                    case EAttrType.AttrCantStiffAir:
                    case EAttrType.AttrCantNormalAttack:
                    case EAttrType.AttrCantSkill:
                    case EAttrType.AttrCantMove:
                    case EAttrType.AttrCantTurn:
                    case EAttrType.AttrCantJump:
                    case EAttrType.AttrCantRush:
                    case EAttrType.AttrCantGravitational:
                    case EAttrType.AttrCantStiffFlow:
                    case EAttrType.AttrCantChangeProfession:
                    case EAttrType.AttrCantInteraction:
                    case EAttrType.AttrCantFallDamage:
                    case EAttrType.AttrCanFlow:
                    case EAttrType.AttrCanGlide:
                    case EAttrType.AttrCanBeHit:
                    case EAttrType.AttrCanLessenHp:
                    case EAttrType.AttrCanIntoCombat:
                    case EAttrType.AttrCantHit:
                    case EAttrType.AttrCanBeHatredTarget:
                    case EAttrType.AttrCanHitNum:
                    case EAttrType.AttrCanHitObj:
                    case EAttrType.AttrCanPathFinding:
                    case EAttrType.AttrCantNormalAttackInput:
                    case EAttrType.AttrCantSkillInput:
                    case EAttrType.AttrCantMoveInput:
                    case EAttrType.AttrCantJumpInput:
                    case EAttrType.AttrCantRushInput:
                    case EAttrType.AttrCantUseToy:
                    case EAttrType.AttrCantRideDown:
                    case EAttrType.AttrCantMultAction:
                    case EAttrType.AttrBlockSkill:
                    case EAttrType.AttrBlockSkillWhiteTags:
                    case EAttrType.AttrTopSummonerSkillSkin:
                    case EAttrType.AttrSummonSkillId:
                    case EAttrType.AttrSceneServStateObjData:
                    case EAttrType.AttrParadeId:
                    case EAttrType.AttrParadeQueueId:
                    case EAttrType.AttrParadeQueueIndex:
                    case EAttrType.AttrBubbleId:
                    case EAttrType.AttrCommunityDataMap:
                    case EAttrType.AttrOwnership:
                    case EAttrType.AttrHomeId:
                    case EAttrType.DecorationInfo:
                    case EAttrType.AttrSubSceneCommunityId:
                    case EAttrType.AttrSubSceneHomeId:
                    case EAttrType.AttrVisitedCharIds:
                    case EAttrType.AttrCohabitant:
                    case EAttrType.AttrAuthorityInfo:
                    case EAttrType.AttrOuterDecorationInfo:
                    case EAttrType.AttrInnerDecorationInfo:
                    case EAttrType.AttrUnlockedAreas:
                    case EAttrType.AttrHousingType:
                    case EAttrType.AttrHouseOwnerCharId:
                    case EAttrType.AttrIsHomelandFriend:
                    case EAttrType.AttrGs:
                    case EAttrType.AttrLastMaxGs:
                    case EAttrType.AttrFightPointTotal:
                    case EAttrType.AttrFightPointAdd:
                    case EAttrType.AttrFightPointExAdd:
                    case EAttrType.AttrFightPointPer:
                    case EAttrType.AttrFightPointExPer:
                    case EAttrType.AttrFightCapability:
                    case EAttrType.AttrFightCapabilityTotal:
                    case EAttrType.AttrFightCapabilityAdd:
                    case EAttrType.AttrFightCapabilityExAdd:
                    case EAttrType.AttrFightCapabilityPer:
                    case EAttrType.AttrFightCapabilityExPer:
                    case EAttrType.AttrSurvivalCapability:
                    case EAttrType.AttrSurvivalCapabilityTotal:
                    case EAttrType.AttrSurvivalCapabilityAdd:
                    case EAttrType.AttrSurvivalCapabilityExAdd:
                    case EAttrType.AttrSurvivalCapabilityPer:
                    case EAttrType.AttrSurvivalCapabilityExPer:
                    case EAttrType.AttrSeasonLevel:
                    case EAttrType.AttrWalkVelocity:
                    case EAttrType.AttrWalkVelocityTotal:
                    case EAttrType.AttrWalkVelocityAdd:
                    case EAttrType.AttrWalkVelocityExAdd:
                    case EAttrType.AttrWalkVelocityPer:
                    case EAttrType.AttrWalkVelocityExPer:
                    case EAttrType.AttrRunVelocity:
                    case EAttrType.AttrRunVelocityTotal:
                    case EAttrType.AttrRunVelocityAdd:
                    case EAttrType.AttrRunVelocityExAdd:
                    case EAttrType.AttrRunVelocityPer:
                    case EAttrType.AttrRunVelocityExPer:
                    case EAttrType.AttrDashVelocity:
                    case EAttrType.AttrDashVelocityTotal:
                    case EAttrType.AttrDashVelocityAdd:
                    case EAttrType.AttrDashVelocityExAdd:
                    case EAttrType.AttrDashVelocityPer:
                    case EAttrType.AttrDashVelocityExPer:
                    case EAttrType.AttrReviveTimeConsumePct:
                    case EAttrType.AttrReviveTimeConsumePcttotal:
                    case EAttrType.AttrReviveTimeConsumePctadd:
                    case EAttrType.AttrReviveTimeConsumePctexAdd:
                    case EAttrType.AttrReviveTimeConsumePctper:
                    case EAttrType.AttrReviveTimeConsumePctexPer:
                    case EAttrType.AttrRideWalkVelocity:
                    case EAttrType.AttrRideWalkVelocityTotal:
                    case EAttrType.AttrRideWalkVelocityAdd:
                    case EAttrType.AttrRideWalkVelocityExAdd:
                    case EAttrType.AttrRideWalkVelocityPer:
                    case EAttrType.AttrRideWalkVelocityExPer:
                    case EAttrType.AttrRideRunVelocity:
                    case EAttrType.AttrRideRunVelocityTotal:
                    case EAttrType.AttrRideRunVelocityAdd:
                    case EAttrType.AttrRideRunVelocityExAdd:
                    case EAttrType.AttrRideRunVelocityPer:
                    case EAttrType.AttrRideRunVelocityExPer:
                    case EAttrType.AttrRideDashVelocity:
                    case EAttrType.AttrRideDashVelocityTotal:
                    case EAttrType.AttrRideDashVelocityAdd:
                    case EAttrType.AttrRideDashVelocityExAdd:
                    case EAttrType.AttrRideDashVelocityPer:
                    case EAttrType.AttrRideDashVelocityExPer:
                    case EAttrType.AttrReviveInterTimeConsumePct:
                    case EAttrType.AttrReviveInterTimeConsumePcttotal:
                    case EAttrType.AttrReviveInterTimeConsumePctadd:
                    case EAttrType.AttrReviveInterTimeConsumePctexAdd:
                    case EAttrType.AttrReviveInterTimeConsumePctper:
                    case EAttrType.AttrReviveInterTimeConsumePctexPer:
                    case EAttrType.AttrStrength:
                    case EAttrType.AttrStrengthTotal:
                    case EAttrType.AttrStrengthAdd:
                    case EAttrType.AttrStrengthExAdd:
                    case EAttrType.AttrStrengthPer:
                    case EAttrType.AttrStrengthExPer:
                    case EAttrType.AttrIntelligence:
                    case EAttrType.AttrIntelligenceTotal:
                    case EAttrType.AttrIntelligenceAdd:
                    case EAttrType.AttrIntelligenceExAdd:
                    case EAttrType.AttrIntelligencePer:
                    case EAttrType.AttrIntelligenceExPer:
                    case EAttrType.AttrDexterity:
                    case EAttrType.AttrDexterityTotal:
                    case EAttrType.AttrDexterityAdd:
                    case EAttrType.AttrDexterityExAdd:
                    case EAttrType.AttrDexterityPer:
                    case EAttrType.AttrDexterityExPer:
                    case EAttrType.AttrVitality:
                    case EAttrType.AttrVitalityTotal:
                    case EAttrType.AttrVitalityAdd:
                    case EAttrType.AttrVitalityExAdd:
                    case EAttrType.AttrVitalityPer:
                    case EAttrType.AttrVitalityExPer:
                    case EAttrType.AttrCriTotal:
                    case EAttrType.AttrCriAdd:
                    case EAttrType.AttrCriExAdd:
                    case EAttrType.AttrCriPer:
                    case EAttrType.AttrCriExPer:
                    case EAttrType.AttrHaste:
                    case EAttrType.AttrHasteTotal:
                    case EAttrType.AttrHasteAdd:
                    case EAttrType.AttrHasteExAdd:
                    case EAttrType.AttrHastePer:
                    case EAttrType.AttrHasteExPer:
                    case EAttrType.AttrLuckTotal:
                    case EAttrType.AttrLuckAdd:
                    case EAttrType.AttrLuckExAdd:
                    case EAttrType.AttrLuckPer:
                    case EAttrType.AttrLuckExPer:
                    case EAttrType.AttrMastery:
                    case EAttrType.AttrMasteryTotal:
                    case EAttrType.AttrMasteryAdd:
                    case EAttrType.AttrMasteryExAdd:
                    case EAttrType.AttrMasteryPer:
                    case EAttrType.AttrMasteryExPer:
                    case EAttrType.AttrVersatility:
                    case EAttrType.AttrVersatilityTotal:
                    case EAttrType.AttrVersatilityAdd:
                    case EAttrType.AttrVersatilityExAdd:
                    case EAttrType.AttrVersatilityPer:
                    case EAttrType.AttrVersatilityExPer:
                    case EAttrType.AttrHit:
                    case EAttrType.AttrHitTotal:
                    case EAttrType.AttrHitAdd:
                    case EAttrType.AttrHitExAdd:
                    case EAttrType.AttrHitPer:
                    case EAttrType.AttrHitExPer:
                    case EAttrType.AttrBlock:
                    case EAttrType.AttrBlockTotal:
                    case EAttrType.AttrBlockAdd:
                    case EAttrType.AttrBlockExAdd:
                    case EAttrType.AttrBlockPer:
                    case EAttrType.AttrBlockExPer:
                    case EAttrType.AttrMaxHpTotal:
                    case EAttrType.AttrMaxHpAdd:
                    case EAttrType.AttrMaxHpExAdd:
                    case EAttrType.AttrMaxHpPer:
                    case EAttrType.AttrMaxHpExPer:
                    case EAttrType.AttrAttack:
                    case EAttrType.AttrAttackTotal:
                    case EAttrType.AttrAttackAdd:
                    case EAttrType.AttrAttackExAdd:
                    case EAttrType.AttrAttackPer:
                    case EAttrType.AttrAttackExPer:
                    case EAttrType.AttrMattack:
                    case EAttrType.AttrMattackTotal:
                    case EAttrType.AttrMattackAdd:
                    case EAttrType.AttrMattackExAdd:
                    case EAttrType.AttrMattackPer:
                    case EAttrType.AttrMattackExPer:
                    case EAttrType.AttrDefense:
                    case EAttrType.AttrDefenseTotal:
                    case EAttrType.AttrDefenseAdd:
                    case EAttrType.AttrDefenseExAdd:
                    case EAttrType.AttrDefensePer:
                    case EAttrType.AttrDefenseExPer:
                    case EAttrType.AttrMdefense:
                    case EAttrType.AttrMdefenseTotal:
                    case EAttrType.AttrMdefenseAdd:
                    case EAttrType.AttrMdefenseExAdd:
                    case EAttrType.AttrMdefensePer:
                    case EAttrType.AttrMdefenseExPer:
                    case EAttrType.AttrIgnoreDefense:
                    case EAttrType.AttrIgnoreDefenseTotal:
                    case EAttrType.AttrIgnoreDefenseAdd:
                    case EAttrType.AttrIgnoreDefenseExAdd:
                    case EAttrType.AttrIgnoreDefensePer:
                    case EAttrType.AttrIgnoreDefenseExPer:
                    case EAttrType.AttrIgnoreMdefense:
                    case EAttrType.AttrIgnoreMdefenseTotal:
                    case EAttrType.AttrIgnoreMdefenseAdd:
                    case EAttrType.AttrIgnoreMdefenseExAdd:
                    case EAttrType.AttrIgnoreMdefensePer:
                    case EAttrType.AttrIgnoreMdefenseExPer:
                    case EAttrType.AttrIgnoreDefensePct:
                    case EAttrType.AttrIgnoreDefensePcttotal:
                    case EAttrType.AttrIgnoreDefensePctadd:
                    case EAttrType.AttrIgnoreDefensePctexAdd:
                    case EAttrType.AttrIgnoreDefensePctper:
                    case EAttrType.AttrIgnoreDefensePctexPer:
                    case EAttrType.AttrIgnoreMdefensePct:
                    case EAttrType.AttrIgnoreMdefensePcttotal:
                    case EAttrType.AttrIgnoreMdefensePctadd:
                    case EAttrType.AttrIgnoreMdefensePctexAdd:
                    case EAttrType.AttrIgnoreMdefensePctper:
                    case EAttrType.AttrIgnoreMdefensePctexPer:
                    case EAttrType.AttrRefineAttack:
                    case EAttrType.AttrRefineAttackTotal:
                    case EAttrType.AttrRefineAttackAdd:
                    case EAttrType.AttrRefineAttackExAdd:
                    case EAttrType.AttrRefineAttackPer:
                    case EAttrType.AttrRefineAttackExPer:
                    case EAttrType.AttrRefineDefense:
                    case EAttrType.AttrRefineDefenseTotal:
                    case EAttrType.AttrRefineDefenseAdd:
                    case EAttrType.AttrRefineDefenseExAdd:
                    case EAttrType.AttrRefineDefensePer:
                    case EAttrType.AttrRefineDefenseExPer:
                    case EAttrType.AttrRefineMattack:
                    case EAttrType.AttrRefineMattackTotal:
                    case EAttrType.AttrRefineMattackAdd:
                    case EAttrType.AttrRefineMattackExAdd:
                    case EAttrType.AttrRefineMattackPer:
                    case EAttrType.AttrRefineMattackExPer:
                    case EAttrType.AttrSeasonWeakness:
                    case EAttrType.AttrSeasonWeaknessTotal:
                    case EAttrType.AttrSeasonWeaknessAdd:
                    case EAttrType.AttrSeasonWeaknessExAdd:
                    case EAttrType.AttrSeasonWeaknessPer:
                    case EAttrType.AttrSeasonWeaknessExPer:
                    case EAttrType.AttrElementAtk:
                    case EAttrType.AttrElementAtkTotal:
                    case EAttrType.AttrElementAtkAdd:
                    case EAttrType.AttrElementAtkExAdd:
                    case EAttrType.AttrElementAtkPer:
                    case EAttrType.AttrElementAtkExPer:
                    case EAttrType.AttrFireAtk:
                    case EAttrType.AttrFireAtkTotal:
                    case EAttrType.AttrFireAtkAdd:
                    case EAttrType.AttrFireAtkExAdd:
                    case EAttrType.AttrFireAtkPer:
                    case EAttrType.AttrFireAtkExPer:
                    case EAttrType.AttrWaterAtk:
                    case EAttrType.AttrWaterAtkTotal:
                    case EAttrType.AttrWaterAtkAdd:
                    case EAttrType.AttrWaterAtkExAdd:
                    case EAttrType.AttrWaterAtkPer:
                    case EAttrType.AttrWaterAtkExPer:
                    case EAttrType.AttrWoodAtk:
                    case EAttrType.AttrWoodAtkTotal:
                    case EAttrType.AttrWoodAtkAdd:
                    case EAttrType.AttrWoodAtkExAdd:
                    case EAttrType.AttrWoodAtkPer:
                    case EAttrType.AttrWoodAtkExPer:
                    case EAttrType.AttrElectricityAtk:
                    case EAttrType.AttrElectricityAtkTotal:
                    case EAttrType.AttrElectricityAtkAdd:
                    case EAttrType.AttrElectricityAtkExAdd:
                    case EAttrType.AttrElectricityAtkPer:
                    case EAttrType.AttrElectricityAtkExPer:
                    case EAttrType.AttrWindAtk:
                    case EAttrType.AttrWindAtkTotal:
                    case EAttrType.AttrWindAtkAdd:
                    case EAttrType.AttrWindAtkExAdd:
                    case EAttrType.AttrWindAtkPer:
                    case EAttrType.AttrWindAtkExPer:
                    case EAttrType.AttrRockAtk:
                    case EAttrType.AttrRockAtkTotal:
                    case EAttrType.AttrRockAtkAdd:
                    case EAttrType.AttrRockAtkExAdd:
                    case EAttrType.AttrRockAtkPer:
                    case EAttrType.AttrRockAtkExPer:
                    case EAttrType.AttrLightAtk:
                    case EAttrType.AttrLightAtkTotal:
                    case EAttrType.AttrLightAtkAdd:
                    case EAttrType.AttrLightAtkExAdd:
                    case EAttrType.AttrLightAtkPer:
                    case EAttrType.AttrLightAtkExPer:
                    case EAttrType.AttrDarkAtk:
                    case EAttrType.AttrDarkAtkTotal:
                    case EAttrType.AttrDarkAtkAdd:
                    case EAttrType.AttrDarkAtkExAdd:
                    case EAttrType.AttrDarkAtkPer:
                    case EAttrType.AttrDarkAtkExPer:
                    case EAttrType.AttrCrit:
                    case EAttrType.AttrCritTotal:
                    case EAttrType.AttrCritAdd:
                    case EAttrType.AttrCritExAdd:
                    case EAttrType.AttrCritPer:
                    case EAttrType.AttrCritExPer:
                    case EAttrType.AttrAttackSpeedPct:
                    case EAttrType.AttrAttackSpeedPcttotal:
                    case EAttrType.AttrAttackSpeedPctadd:
                    case EAttrType.AttrAttackSpeedPctexAdd:
                    case EAttrType.AttrAttackSpeedPctper:
                    case EAttrType.AttrAttackSpeedPctexPer:
                    case EAttrType.AttrCastSpeedPct:
                    case EAttrType.AttrCastSpeedPcttotal:
                    case EAttrType.AttrCastSpeedPctadd:
                    case EAttrType.AttrCastSpeedPctexAdd:
                    case EAttrType.AttrCastSpeedPctper:
                    case EAttrType.AttrCastSpeedPctexPer:
                    case EAttrType.AttrChargeSpeedPct:
                    case EAttrType.AttrChargeSpeedPcttotal:
                    case EAttrType.AttrChargeSpeedPctadd:
                    case EAttrType.AttrChargeSpeedPctexAdd:
                    case EAttrType.AttrChargeSpeedPctper:
                    case EAttrType.AttrChargeSpeedPctexPer:
                    case EAttrType.AttrSkillCd:
                    case EAttrType.AttrSkillCdtotal:
                    case EAttrType.AttrSkillCdadd:
                    case EAttrType.AttrSkillCdexAdd:
                    case EAttrType.AttrSkillCdper:
                    case EAttrType.AttrSkillCdexPer:
                    case EAttrType.AttrSkillCdpct:
                    case EAttrType.AttrSkillCdpcttotal:
                    case EAttrType.AttrSkillCdpctadd:
                    case EAttrType.AttrSkillCdpctexAdd:
                    case EAttrType.AttrSkillCdpctper:
                    case EAttrType.AttrSkillCdpctexPer:
                    case EAttrType.AttrDotTime:
                    case EAttrType.AttrDotTimeTotal:
                    case EAttrType.AttrDotTimeAdd:
                    case EAttrType.AttrDotTimeExAdd:
                    case EAttrType.AttrDotTimePer:
                    case EAttrType.AttrDotTimeExPer:
                    case EAttrType.AttrLuckyStrikeProb:
                    case EAttrType.AttrLuckyStrikeProbTotal:
                    case EAttrType.AttrLuckyStrikeProbAdd:
                    case EAttrType.AttrLuckyStrikeProbExAdd:
                    case EAttrType.AttrLuckyStrikeProbPer:
                    case EAttrType.AttrLuckyStrikeProbExPer:
                    case EAttrType.AttrHeal:
                    case EAttrType.AttrHealTotal:
                    case EAttrType.AttrHealAdd:
                    case EAttrType.AttrHealExAdd:
                    case EAttrType.AttrHealPer:
                    case EAttrType.AttrHealExPer:
                    case EAttrType.AttrHealed:
                    case EAttrType.AttrHealedTotal:
                    case EAttrType.AttrHealedAdd:
                    case EAttrType.AttrHealedExAdd:
                    case EAttrType.AttrHealedPer:
                    case EAttrType.AttrHealedExPer:
                    case EAttrType.AttrShieldAddPct:
                    case EAttrType.AttrShieldAddPcttotal:
                    case EAttrType.AttrShieldAddPctadd:
                    case EAttrType.AttrShieldAddPctexAdd:
                    case EAttrType.AttrShieldAddPctper:
                    case EAttrType.AttrShieldAddPctexPer:
                    case EAttrType.AttrShieldGainPct:
                    case EAttrType.AttrShieldGainPcttotal:
                    case EAttrType.AttrShieldGainPctadd:
                    case EAttrType.AttrShieldGainPctexAdd:
                    case EAttrType.AttrShieldGainPctper:
                    case EAttrType.AttrShieldGainPctexPer:
                    case EAttrType.AttrStunnedDamagePct:
                    case EAttrType.AttrStunnedDamagePcttotal:
                    case EAttrType.AttrStunnedDamagePctadd:
                    case EAttrType.AttrStunnedDamagePctexAdd:
                    case EAttrType.AttrStunnedDamagePctper:
                    case EAttrType.AttrStunnedDamagePctexPer:
                    case EAttrType.AttrExtDamInc:
                    case EAttrType.AttrExtDamIncTotal:
                    case EAttrType.AttrExtDamIncAdd:
                    case EAttrType.AttrExtDamIncExAdd:
                    case EAttrType.AttrExtDamIncPer:
                    case EAttrType.AttrExtDamIncExPer:
                    case EAttrType.AttrExtDamRes:
                    case EAttrType.AttrExtDamResTotal:
                    case EAttrType.AttrExtDamResAdd:
                    case EAttrType.AttrExtDamResExAdd:
                    case EAttrType.AttrExtDamResPer:
                    case EAttrType.AttrExtDamResExPer:
                    case EAttrType.AttrDpsOwnEffectStr:
                    case EAttrType.AttrDpsOwnEffectStrTotal:
                    case EAttrType.AttrDpsOwnEffectStrAdd:
                    case EAttrType.AttrDpsOwnEffectStrExAdd:
                    case EAttrType.AttrDpsOwnEffectStrPer:
                    case EAttrType.AttrDpsOwnEffectStrExPer:
                    case EAttrType.AttrRainbowDamage:
                    case EAttrType.AttrRainbowDamageTotal:
                    case EAttrType.AttrRainbowDamageAdd:
                    case EAttrType.AttrRainbowDamageExAdd:
                    case EAttrType.AttrRainbowDamagePer:
                    case EAttrType.AttrRainbowDamageExPer:
                    case EAttrType.AttrSuppressDamInc:
                    case EAttrType.AttrSuppressDamIncTotal:
                    case EAttrType.AttrSuppressDamIncAdd:
                    case EAttrType.AttrSuppressDamIncExAdd:
                    case EAttrType.AttrSuppressDamIncPer:
                    case EAttrType.AttrSuppressDamIncExPer:
                    case EAttrType.AttrSuppressDamRes:
                    case EAttrType.AttrSuppressDamResTotal:
                    case EAttrType.AttrSuppressDamResAdd:
                    case EAttrType.AttrSuppressDamResExAdd:
                    case EAttrType.AttrSuppressDamResPer:
                    case EAttrType.AttrSuppressDamResExPer:
                    case EAttrType.AttrInspirePct:
                    case EAttrType.AttrInspirePctTotal:
                    case EAttrType.AttrInspirePctAdd:
                    case EAttrType.AttrInspirePctExAdd:
                    case EAttrType.AttrInspirePctPer:
                    case EAttrType.AttrInspirePctExPer:
                    case EAttrType.AttrHateRatePtc:
                    case EAttrType.AttrHateRatePtctotal:
                    case EAttrType.AttrHateRatePtcadd:
                    case EAttrType.AttrHateRatePtcexAdd:
                    case EAttrType.AttrHateRatePtcper:
                    case EAttrType.AttrHateRatePtcexPer:
                    case EAttrType.AttrHastePct:
                    case EAttrType.AttrHastePctTotal:
                    case EAttrType.AttrHastePctAdd:
                    case EAttrType.AttrHastePctExAdd:
                    case EAttrType.AttrHastePctPer:
                    case EAttrType.AttrHastePctExPer:
                    case EAttrType.AttrMasteryPct:
                    case EAttrType.AttrMasteryPctTotal:
                    case EAttrType.AttrMasteryPctAdd:
                    case EAttrType.AttrMasteryPctExAdd:
                    case EAttrType.AttrMasteryPctPer:
                    case EAttrType.AttrMasteryPctExPer:
                    case EAttrType.AttrVersatilityPct:
                    case EAttrType.AttrVersatilityPctTotal:
                    case EAttrType.AttrVersatilityPctAdd:
                    case EAttrType.AttrVersatilityPctExAdd:
                    case EAttrType.AttrVersatilityPctPer:
                    case EAttrType.AttrVersatilityPctExPer:
                    case EAttrType.AttrCdAcceleratePct:
                    case EAttrType.AttrCdAcceleratePctTotal:
                    case EAttrType.AttrCdAcceleratePctAdd:
                    case EAttrType.AttrCdAcceleratePctExAdd:
                    case EAttrType.AttrCdAcceleratePctPer:
                    case EAttrType.AttrCdAcceleratePctExPer:
                    case EAttrType.AttrBlockPct:
                    case EAttrType.AttrBlockPctTotal:
                    case EAttrType.AttrBlockPctAdd:
                    case EAttrType.AttrBlockPctExAdd:
                    case EAttrType.AttrBlockPctPer:
                    case EAttrType.AttrBlockPctExPer:
                    case EAttrType.AttrFightResCdSpeedPct:
                    case EAttrType.AttrFightResCdSpeedPctTotal:
                    case EAttrType.AttrFightResCdSpeedPctAdd:
                    case EAttrType.AttrFightResCdSpeedPctExAdd:
                    case EAttrType.AttrFightResCdSpeedPctPer:
                    case EAttrType.AttrFightResCdSpeedPctExPer:
                    case EAttrType.AttrPetAttackSpeedPct:
                    case EAttrType.AttrPetAttackSpeedPcttotal:
                    case EAttrType.AttrPetAttackSpeedPctadd:
                    case EAttrType.AttrPetAttackSpeedPctexAdd:
                    case EAttrType.AttrPetAttackSpeedPctper:
                    case EAttrType.AttrPetAttackSpeedPctexPer:
                    case EAttrType.AttrCritDamage:
                    case EAttrType.AttrCritDamageTotal:
                    case EAttrType.AttrCritDamageAdd:
                    case EAttrType.AttrCritDamageExAdd:
                    case EAttrType.AttrCritDamagePer:
                    case EAttrType.AttrCritDamageExPer:
                    case EAttrType.AttrCritDamageRes:
                    case EAttrType.AttrCritDamageResTotal:
                    case EAttrType.AttrCritDamageResAdd:
                    case EAttrType.AttrCritDamageResExAdd:
                    case EAttrType.AttrCritDamageResPer:
                    case EAttrType.AttrCritDamageResExPer:
                    case EAttrType.AttrLuckDamInc:
                    case EAttrType.AttrLuckDamIncTotal:
                    case EAttrType.AttrLuckDamIncAdd:
                    case EAttrType.AttrLuckDamIncExAdd:
                    case EAttrType.AttrLuckDamIncPer:
                    case EAttrType.AttrLuckDamIncExPer:
                    case EAttrType.AttrBlockDamRes:
                    case EAttrType.AttrBlockDamResTotal:
                    case EAttrType.AttrBlockDamResAdd:
                    case EAttrType.AttrBlockDamResExAdd:
                    case EAttrType.AttrBlockDamResPer:
                    case EAttrType.AttrBlockDamResExPer:
                    case EAttrType.AttrDamInc:
                    case EAttrType.AttrDamIncTotal:
                    case EAttrType.AttrDamIncAdd:
                    case EAttrType.AttrDamIncExAdd:
                    case EAttrType.AttrDamIncPer:
                    case EAttrType.AttrDamIncExPer:
                    case EAttrType.AttrDamRes:
                    case EAttrType.AttrDamResTotal:
                    case EAttrType.AttrDamResAdd:
                    case EAttrType.AttrDamResExAdd:
                    case EAttrType.AttrDamResPer:
                    case EAttrType.AttrDamResExPer:
                    case EAttrType.AttrMdamInc:
                    case EAttrType.AttrMdamIncTotal:
                    case EAttrType.AttrMdamIncAdd:
                    case EAttrType.AttrMdamIncExAdd:
                    case EAttrType.AttrMdamIncPer:
                    case EAttrType.AttrMdamIncExPer:
                    case EAttrType.AttrMdamRes:
                    case EAttrType.AttrMdamResTotal:
                    case EAttrType.AttrMdamResAdd:
                    case EAttrType.AttrMdamResExAdd:
                    case EAttrType.AttrMdamResPer:
                    case EAttrType.AttrMdamResExPer:
                    case EAttrType.AttrNearDamage:
                    case EAttrType.AttrNearDamageTotal:
                    case EAttrType.AttrNearDamageAdd:
                    case EAttrType.AttrNearDamageExAdd:
                    case EAttrType.AttrNearDamagePer:
                    case EAttrType.AttrNearDamageExPer:
                    case EAttrType.AttrNearDamageReduction:
                    case EAttrType.AttrNearDamageReductionTotal:
                    case EAttrType.AttrNearDamageReductionAdd:
                    case EAttrType.AttrNearDamageReductionExAdd:
                    case EAttrType.AttrNearDamageReductionPer:
                    case EAttrType.AttrNearDamageReductionExPer:
                    case EAttrType.AttrFarDamage:
                    case EAttrType.AttrFarDamageTotal:
                    case EAttrType.AttrFarDamageAdd:
                    case EAttrType.AttrFarDamageExAdd:
                    case EAttrType.AttrFarDamagePer:
                    case EAttrType.AttrFarDamageExPer:
                    case EAttrType.AttrFarDamageReduction:
                    case EAttrType.AttrFarDamageReductionTotal:
                    case EAttrType.AttrFarDamageReductionAdd:
                    case EAttrType.AttrFarDamageReductionExAdd:
                    case EAttrType.AttrFarDamageReductionPer:
                    case EAttrType.AttrFarDamageReductionExPer:
                    case EAttrType.AttrBossDamInc:
                    case EAttrType.AttrBossDamIncTotal:
                    case EAttrType.AttrBossDamIncAdd:
                    case EAttrType.AttrBossDamIncExAdd:
                    case EAttrType.AttrBossDamIncPer:
                    case EAttrType.AttrBossDamIncExPer:
                    case EAttrType.AttrBossDamRes:
                    case EAttrType.AttrBossDamResTotal:
                    case EAttrType.AttrBossDamResAdd:
                    case EAttrType.AttrBossDamResExAdd:
                    case EAttrType.AttrBossDamResPer:
                    case EAttrType.AttrBossDamResExPer:
                    case EAttrType.AttrShieldDamagePct:
                    case EAttrType.AttrShieldDamagePcttotal:
                    case EAttrType.AttrShieldDamagePctadd:
                    case EAttrType.AttrShieldDamagePctexAdd:
                    case EAttrType.AttrShieldDamagePctper:
                    case EAttrType.AttrShieldDamagePctexPer:
                    case EAttrType.AttrShieldDamageReductionPct:
                    case EAttrType.AttrShieldDamageReductionPcttotal:
                    case EAttrType.AttrShieldDamageReductionPctadd:
                    case EAttrType.AttrShieldDamageReductionPctexAdd:
                    case EAttrType.AttrShieldDamageReductionPctper:
                    case EAttrType.AttrShieldDamageReductionPctexPer:
                    case EAttrType.AttrOtherDamInc:
                    case EAttrType.AttrOtherDamIncTotal:
                    case EAttrType.AttrOtherDamIncAdd:
                    case EAttrType.AttrOtherDamIncExAdd:
                    case EAttrType.AttrOtherDamIncTper:
                    case EAttrType.AttrOtherDamIncExPer:
                    case EAttrType.AttrOtherDamRes:
                    case EAttrType.AttrOtherDamResTotal:
                    case EAttrType.AttrOtherDamResAdd:
                    case EAttrType.AttrOtherDamResExAdd:
                    case EAttrType.AttrOtherDamResTper:
                    case EAttrType.AttrOtherDamResExPer:
                    case EAttrType.AttrSeasonDamInc:
                    case EAttrType.AttrSeasonDamIncTotal:
                    case EAttrType.AttrSeasonDamIncAdd:
                    case EAttrType.AttrSeasonDamIncExAdd:
                    case EAttrType.AttrSeasonDamIncTper:
                    case EAttrType.AttrSeasonDamIncExPer:
                    case EAttrType.AttrSeasonDamRes:
                    case EAttrType.AttrSeasonDamResTotal:
                    case EAttrType.AttrSeasonDamResAdd:
                    case EAttrType.AttrSeasonDamResExAdd:
                    case EAttrType.AttrSeasonDamResTper:
                    case EAttrType.AttrSeasonDamResExPer:
                    case EAttrType.AttrMultipliesDamPct:
                    case EAttrType.AttrMultipliesDamPctTotal:
                    case EAttrType.AttrMultipliesDamPctAdd:
                    case EAttrType.AttrMultipliesDamPctExAdd:
                    case EAttrType.AttrMultipliesDamPctTper:
                    case EAttrType.AttrMultipliesDamPctExPer:
                    case EAttrType.AttrLuckHealInc:
                    case EAttrType.AttrLuckHealIncTotal:
                    case EAttrType.AttrLuckHealIncAdd:
                    case EAttrType.AttrLuckHealIncExAdd:
                    case EAttrType.AttrLuckHealIncPer:
                    case EAttrType.AttrLuckHealIncExPer:
                    case EAttrType.AttrPetDamInc:
                    case EAttrType.AttrPetDamIncTotal:
                    case EAttrType.AttrPetDamIncAdd:
                    case EAttrType.AttrPetDamIncExAdd:
                    case EAttrType.AttrPetDamIncPer:
                    case EAttrType.AttrPetDamIncExPer:
                    case EAttrType.AttrCritHeal:
                    case EAttrType.AttrCritHealTotal:
                    case EAttrType.AttrCritHealAdd:
                    case EAttrType.AttrCritHealExAdd:
                    case EAttrType.AttrCritHealPer:
                    case EAttrType.AttrCritHealExPer:
                    case EAttrType.AttrSpDamInc:
                    case EAttrType.AttrSpDamIncTotal:
                    case EAttrType.AttrSpDamIncAdd:
                    case EAttrType.AttrSpDamIncExAdd:
                    case EAttrType.AttrSpDamIncPer:
                    case EAttrType.AttrSpDamIncExPer:
                    case EAttrType.AttrSpDamRes:
                    case EAttrType.AttrSpDamResTotal:
                    case EAttrType.AttrSpDamResAdd:
                    case EAttrType.AttrSpDamResExAdd:
                    case EAttrType.AttrSpDamResPer:
                    case EAttrType.AttrSpDamResExPer:
                    case EAttrType.AttrHealBanPct:
                    case EAttrType.AttrHealBanPctTotal:
                    case EAttrType.AttrHealBanPctAdd:
                    case EAttrType.AttrHealBanPctExAdd:
                    case EAttrType.AttrHealBanPctPer:
                    case EAttrType.AttrHealBanPctExPer:
                    case EAttrType.AttrHealedBanPct:
                    case EAttrType.AttrHealedBanPctTotal:
                    case EAttrType.AttrHealedBanPctAdd:
                    case EAttrType.AttrHealedBanPctExAdd:
                    case EAttrType.AttrHealedBanPctPer:
                    case EAttrType.AttrHealedBanPctExPer:
                    case EAttrType.AttrPhyPowerToDam:
                    case EAttrType.AttrPhyPowerToDamTotal:
                    case EAttrType.AttrPhyPowerToDamAdd:
                    case EAttrType.AttrPhyPowerToDamExAdd:
                    case EAttrType.AttrPhyPowerToDamPer:
                    case EAttrType.AttrPhyPowerToDamExPer:
                    case EAttrType.AttrMagPowerToDam:
                    case EAttrType.AttrMagPowerToDamTotal:
                    case EAttrType.AttrMagPowerToDamAdd:
                    case EAttrType.AttrMagPowerToDamExAdd:
                    case EAttrType.AttrMagPowerToDamPer:
                    case EAttrType.AttrMagPowerToDamExPer:
                    case EAttrType.AttrElementPower:
                    case EAttrType.AttrElementPowerTotal:
                    case EAttrType.AttrElementPowerAdd:
                    case EAttrType.AttrElementPowerExAdd:
                    case EAttrType.AttrElementPowerPer:
                    case EAttrType.AttrElementPowerExPer:
                    case EAttrType.AttrFirePower:
                    case EAttrType.AttrFirePowerTotal:
                    case EAttrType.AttrFirePowerAdd:
                    case EAttrType.AttrFirePowerExAdd:
                    case EAttrType.AttrFirePowerPer:
                    case EAttrType.AttrFirePowerExPer:
                    case EAttrType.AttrWaterPower:
                    case EAttrType.AttrWaterPowerTotal:
                    case EAttrType.AttrWaterPowerAdd:
                    case EAttrType.AttrWaterPowerExAdd:
                    case EAttrType.AttrWaterPowerPer:
                    case EAttrType.AttrWaterPowerExPer:
                    case EAttrType.AttrWoodPower:
                    case EAttrType.AttrWoodPowerTotal:
                    case EAttrType.AttrWoodPowerAdd:
                    case EAttrType.AttrWoodPowerExAdd:
                    case EAttrType.AttrWoodPowerPer:
                    case EAttrType.AttrWoodPowerExPer:
                    case EAttrType.AttrElectricityPower:
                    case EAttrType.AttrElectricityPowerTotal:
                    case EAttrType.AttrElectricityPowerAdd:
                    case EAttrType.AttrElectricityPowerExAdd:
                    case EAttrType.AttrElectricityPowerPer:
                    case EAttrType.AttrElectricityPowerExPer:
                    case EAttrType.AttrWindPower:
                    case EAttrType.AttrWindPowerTotal:
                    case EAttrType.AttrWindPowerAdd:
                    case EAttrType.AttrWindPowerExAdd:
                    case EAttrType.AttrWindPowerPer:
                    case EAttrType.AttrWindPowerExPer:
                    case EAttrType.AttrRockPower:
                    case EAttrType.AttrRockPowerTotal:
                    case EAttrType.AttrRockPowerAdd:
                    case EAttrType.AttrRockPowerExAdd:
                    case EAttrType.AttrRockPowerPer:
                    case EAttrType.AttrRockPowerExPer:
                    case EAttrType.AttrLightPower:
                    case EAttrType.AttrLightPowerTotal:
                    case EAttrType.AttrLightPowerAdd:
                    case EAttrType.AttrLightPowerExAdd:
                    case EAttrType.AttrLightPowerPer:
                    case EAttrType.AttrLightPowerExPer:
                    case EAttrType.AttrDarkPower:
                    case EAttrType.AttrDarkPowerTotal:
                    case EAttrType.AttrDarkPowerAdd:
                    case EAttrType.AttrDarkPowerExAdd:
                    case EAttrType.AttrDarkPowerPer:
                    case EAttrType.AttrDarkPowerExPer:
                    case EAttrType.AttrElementDamage:
                    case EAttrType.AttrElementDamageTotal:
                    case EAttrType.AttrElementDamageAdd:
                    case EAttrType.AttrElementDamageExAdd:
                    case EAttrType.AttrElementDamagePer:
                    case EAttrType.AttrElementDamageExPer:
                    case EAttrType.AttrFireDamage:
                    case EAttrType.AttrFireDamageTotal:
                    case EAttrType.AttrFireDamageAdd:
                    case EAttrType.AttrFireDamageExAdd:
                    case EAttrType.AttrFireDamagePer:
                    case EAttrType.AttrFireDamageExPer:
                    case EAttrType.AttrWaterDamage:
                    case EAttrType.AttrWaterDamageTotal:
                    case EAttrType.AttrWaterDamageAdd:
                    case EAttrType.AttrWaterDamageExAdd:
                    case EAttrType.AttrWaterDamagePer:
                    case EAttrType.AttrWaterDamageExPer:
                    case EAttrType.AttrWoodDamage:
                    case EAttrType.AttrWoodDamageTotal:
                    case EAttrType.AttrWoodDamageAdd:
                    case EAttrType.AttrWoodDamageExAdd:
                    case EAttrType.AttrWoodDamagePer:
                    case EAttrType.AttrWoodDamageExPer:
                    case EAttrType.AttrElectricityDamage:
                    case EAttrType.AttrElectricityDamageTotal:
                    case EAttrType.AttrElectricityDamageAdd:
                    case EAttrType.AttrElectricityDamageExAdd:
                    case EAttrType.AttrElectricityDamagePer:
                    case EAttrType.AttrElectricityDamageExPer:
                    case EAttrType.AttrWindDamage:
                    case EAttrType.AttrWindDamageTotal:
                    case EAttrType.AttrWindDamageAdd:
                    case EAttrType.AttrWindDamageExAdd:
                    case EAttrType.AttrWindDamagePer:
                    case EAttrType.AttrWindDamageExPer:
                    case EAttrType.AttrRockDamage:
                    case EAttrType.AttrRockDamageTotal:
                    case EAttrType.AttrRockDamageAdd:
                    case EAttrType.AttrRockDamageExAdd:
                    case EAttrType.AttrRockDamagePer:
                    case EAttrType.AttrRockDamageExPer:
                    case EAttrType.AttrLightDamage:
                    case EAttrType.AttrLightDamageTotal:
                    case EAttrType.AttrLightDamageAdd:
                    case EAttrType.AttrLightDamageExAdd:
                    case EAttrType.AttrLightDamagePer:
                    case EAttrType.AttrLightDamageExPer:
                    case EAttrType.AttrDarkDamage:
                    case EAttrType.AttrDarkDamageTotal:
                    case EAttrType.AttrDarkDamageAdd:
                    case EAttrType.AttrDarkDamageExAdd:
                    case EAttrType.AttrDarkDamagePer:
                    case EAttrType.AttrDarkDamageExPer:
                    case EAttrType.AttrElementDefense:
                    case EAttrType.AttrElementDefenseTotal:
                    case EAttrType.AttrElementDefenseAdd:
                    case EAttrType.AttrElementDefenseExAdd:
                    case EAttrType.AttrElementDefensePer:
                    case EAttrType.AttrElementDefenseExPer:
                    case EAttrType.AttrFireDefense:
                    case EAttrType.AttrFireDefenseTotal:
                    case EAttrType.AttrFireDefenseAdd:
                    case EAttrType.AttrFireDefenseExAdd:
                    case EAttrType.AttrFireDefensePer:
                    case EAttrType.AttrFireDefenseExPer:
                    case EAttrType.AttrWaterDefense:
                    case EAttrType.AttrWaterDefenseTotal:
                    case EAttrType.AttrWaterDefenseAdd:
                    case EAttrType.AttrWaterDefenseExAdd:
                    case EAttrType.AttrWaterDefensePer:
                    case EAttrType.AttrWaterDefenseExPer:
                    case EAttrType.AttrWoodDefense:
                    case EAttrType.AttrWoodDefenseTotal:
                    case EAttrType.AttrWoodDefenseAdd:
                    case EAttrType.AttrWoodDefenseExAdd:
                    case EAttrType.AttrWoodDefensePer:
                    case EAttrType.AttrWoodDefenseExPer:
                    case EAttrType.AttrElectricityDefense:
                    case EAttrType.AttrElectricityDefenseTotal:
                    case EAttrType.AttrElectricityDefenseAdd:
                    case EAttrType.AttrElectricityDefenseExAdd:
                    case EAttrType.AttrElectricityDefensePer:
                    case EAttrType.AttrElectricityDefenseExPer:
                    case EAttrType.AttrWindDefense:
                    case EAttrType.AttrWindDefenseTotal:
                    case EAttrType.AttrWindDefenseAdd:
                    case EAttrType.AttrWindDefenseExAdd:
                    case EAttrType.AttrWindDefensePer:
                    case EAttrType.AttrWindDefenseExPer:
                    case EAttrType.AttrRockDefense:
                    case EAttrType.AttrRockDefenseTotal:
                    case EAttrType.AttrRockDefenseAdd:
                    case EAttrType.AttrRockDefenseExAdd:
                    case EAttrType.AttrRockDefensePer:
                    case EAttrType.AttrRockDefenseExPer:
                    case EAttrType.AttrLightDefense:
                    case EAttrType.AttrLightDefenseTotal:
                    case EAttrType.AttrLightDefenseAdd:
                    case EAttrType.AttrLightDefenseExAdd:
                    case EAttrType.AttrLightDefensePer:
                    case EAttrType.AttrLightDefenseExPer:
                    case EAttrType.AttrDarkDefense:
                    case EAttrType.AttrDarkDefenseTotal:
                    case EAttrType.AttrDarkDefenseAdd:
                    case EAttrType.AttrDarkDefenseExAdd:
                    case EAttrType.AttrDarkDefensePer:
                    case EAttrType.AttrDarkDefenseExPer:
                    case EAttrType.AttrElementDamRes:
                    case EAttrType.AttrElementDamResTotal:
                    case EAttrType.AttrElementDamResAdd:
                    case EAttrType.AttrElementDamResExAdd:
                    case EAttrType.AttrElementDamResPer:
                    case EAttrType.AttrElementDamResExPer:
                    case EAttrType.AttrFireDamageReduction:
                    case EAttrType.AttrFireDamageReductionTotal:
                    case EAttrType.AttrFireDamageReductionAdd:
                    case EAttrType.AttrFireDamageReductionExAdd:
                    case EAttrType.AttrFireDamageReductionPer:
                    case EAttrType.AttrFireDamageReductionExPer:
                    case EAttrType.AttrWaterDamageReduction:
                    case EAttrType.AttrWaterDamageReductionTotal:
                    case EAttrType.AttrWaterDamageReductionAdd:
                    case EAttrType.AttrWaterDamageReductionExAdd:
                    case EAttrType.AttrWaterDamageReductionPer:
                    case EAttrType.AttrWaterDamageReductionExPer:
                    case EAttrType.AttrWoodDamageReduction:
                    case EAttrType.AttrWoodDamageReductionTotal:
                    case EAttrType.AttrWoodDamageReductionAdd:
                    case EAttrType.AttrWoodDamageReductionExAdd:
                    case EAttrType.AttrWoodDamageReductionPer:
                    case EAttrType.AttrWoodDamageReductionExPer:
                    case EAttrType.AttrElectricityDamageReduction:
                    case EAttrType.AttrElectricityDamageReductionTotal:
                    case EAttrType.AttrElectricityDamageReductionAdd:
                    case EAttrType.AttrElectricityDamageReductionExAdd:
                    case EAttrType.AttrElectricityDamageReductionPer:
                    case EAttrType.AttrElectricityDamageReductionExPer:
                    case EAttrType.AttrWindDamageReduction:
                    case EAttrType.AttrWindDamageReductionTotal:
                    case EAttrType.AttrWindDamageReductionAdd:
                    case EAttrType.AttrWindDamageReductionExAdd:
                    case EAttrType.AttrWindDamageReductionPer:
                    case EAttrType.AttrWindDamageReductionExPer:
                    case EAttrType.AttrRockDamageReduction:
                    case EAttrType.AttrRockDamageReductionTotal:
                    case EAttrType.AttrRockDamageReductionAdd:
                    case EAttrType.AttrRockDamageReductionExAdd:
                    case EAttrType.AttrRockDamageReductionPer:
                    case EAttrType.AttrRockDamageReductionExPer:
                    case EAttrType.AttrLightDamageReduction:
                    case EAttrType.AttrLightDamageReductionTotal:
                    case EAttrType.AttrLightDamageReductionAdd:
                    case EAttrType.AttrLightDamageReductionExAdd:
                    case EAttrType.AttrLightDamageReductionPer:
                    case EAttrType.AttrLightDamageReductionExPer:
                    case EAttrType.AttrDarkDamageReduction:
                    case EAttrType.AttrDarkDamageReductionTotal:
                    case EAttrType.AttrDarkDamageReductionAdd:
                    case EAttrType.AttrDarkDamageReductionExAdd:
                    case EAttrType.AttrDarkDamageReductionPer:
                    case EAttrType.AttrDarkDamageReductionExPer:
                    case EAttrType.AttrOriginEnergy:
                    case EAttrType.AttrMaxOriginEnergy:
                    case EAttrType.AttrMaxOriginEnergyTotal:
                    case EAttrType.AttrMaxOriginEnergyAdd:
                    case EAttrType.AttrMaxOriginEnergyExAdd:
                    case EAttrType.AttrMaxOriginEnergyPer:
                    case EAttrType.AttrMaxOriginEnergyExPer:
                    case EAttrType.AttrOriginEnergyConsumeRate:
                    case EAttrType.AttrParkourStandbyOriginEnergyRecovery:
                    case EAttrType.AttrParkourStandbyOriginEnergyRecoveryTotal:
                    case EAttrType.AttrParkourStandbyOriginEnergyRecoveryAdd:
                    case EAttrType.AttrParkourStandbyOriginEnergyRecoveryExAdd:
                    case EAttrType.AttrParkourStandbyOriginEnergyRecoveryPer:
                    case EAttrType.AttrParkourStandbyOriginEnergyRecoveryExPer:
                    case EAttrType.AttrParkourOriginEnergyRecovery:
                    case EAttrType.AttrParkourOriginEnergyRecoveryTotal:
                    case EAttrType.AttrParkourOriginEnergyRecoveryAdd:
                    case EAttrType.AttrParkourOriginEnergyRecoveryExAdd:
                    case EAttrType.AttrParkourOriginEnergyRecoveryPer:
                    case EAttrType.AttrParkourOriginEnergyRecoveryExPer:
                    case EAttrType.AttrParkourRunPhaseOneAcceleration:
                    case EAttrType.AttrParkourRunPhaseOneAccelerationTotal:
                    case EAttrType.AttrParkourRunPhaseOneAccelerationAdd:
                    case EAttrType.AttrParkourRunPhaseOneAccelerationExAdd:
                    case EAttrType.AttrParkourRunPhaseOneAccelerationPer:
                    case EAttrType.AttrParkourRunPhaseOneAccelerationExPer:
                    case EAttrType.AttrParkourRunPhaseOneSpeedLimit:
                    case EAttrType.AttrParkourRunPhaseOneSpeedLimitTotal:
                    case EAttrType.AttrParkourRunPhaseOneSpeedLimitAdd:
                    case EAttrType.AttrParkourRunPhaseOneSpeedLimitExAdd:
                    case EAttrType.AttrParkourRunPhaseOneSpeedLimitPer:
                    case EAttrType.AttrParkourRunPhaseOneSpeedLimitExPer:
                    case EAttrType.AttrParkourRunPhaseTwoAcceleration:
                    case EAttrType.AttrParkourRunPhaseTwoAccelerationTotal:
                    case EAttrType.AttrParkourRunPhaseTwoAccelerationAdd:
                    case EAttrType.AttrParkourRunPhaseTwoAccelerationExAdd:
                    case EAttrType.AttrParkourRunPhaseTwoAccelerationPer:
                    case EAttrType.AttrParkourRunPhaseTwoAccelerationExPer:
                    case EAttrType.AttrParkourRunPhaseTwoSpeedLimit:
                    case EAttrType.AttrParkourRunPhaseTwoSpeedLimitTotal:
                    case EAttrType.AttrParkourRunPhaseTwoSpeedLimitAdd:
                    case EAttrType.AttrParkourRunPhaseTwoSpeedLimitExAdd:
                    case EAttrType.AttrParkourRunPhaseTwoSpeedLimitPer:
                    case EAttrType.AttrParkourRunPhaseTwoSpeedLimitExPer:
                    case EAttrType.AttrParkourRunPhaseThreeAcceleration:
                    case EAttrType.AttrParkourRunPhaseThreeAccelerationTotal:
                    case EAttrType.AttrParkourRunPhaseThreeAccelerationAdd:
                    case EAttrType.AttrParkourRunPhaseThreeAccelerationExAdd:
                    case EAttrType.AttrParkourRunPhaseThreeAccelerationPer:
                    case EAttrType.AttrParkourRunPhaseThreeAccelerationExPer:
                    case EAttrType.AttrParkourRunPhaseThreeSpeedLimit:
                    case EAttrType.AttrParkourRunPhaseThreeSpeedLimitTotal:
                    case EAttrType.AttrParkourRunPhaseThreeSpeedLimitAdd:
                    case EAttrType.AttrParkourRunPhaseThreeSpeedLimitExAdd:
                    case EAttrType.AttrParkourRunPhaseThreeSpeedLimitPer:
                    case EAttrType.AttrParkourRunPhaseThreeSpeedLimitExPer:
                    case EAttrType.AttrInBattleParkourStandbyOriginEnergyRecovery:
                    case EAttrType.AttrInBattleParkourStandbyOriginEnergyRecoveryTotal:
                    case EAttrType.AttrInBattleParkourStandbyOriginEnergyRecoveryAdd:
                    case EAttrType.AttrInBattleParkourStandbyOriginEnergyRecoveryExAdd:
                    case EAttrType.AttrInBattleParkourStandbyOriginEnergyRecoveryPer:
                    case EAttrType.AttrInBattleParkourStandbyOriginEnergyRecoveryExPer:
                    case EAttrType.AttrInBattleParkourOriginEnergyRecovery:
                    case EAttrType.AttrInBattleParkourOriginEnergyRecoveryTotal:
                    case EAttrType.AttrInBattleParkourOriginEnergyRecoveryAdd:
                    case EAttrType.AttrInBattleParkourOriginEnergyRecoveryExAdd:
                    case EAttrType.AttrInBattleParkourOriginEnergyRecoveryPer:
                    case EAttrType.AttrInBattleParkourOriginEnergyRecoveryExPer:
                    case EAttrType.AttrFallDamageReduction:
                    case EAttrType.AttrDelayDie:
                    case EAttrType.AttrFightResourceIds:
                    case EAttrType.AttrFightResources:
                    case EAttrType.AttrFightResNoUp:
                    case EAttrType.AttrFightResNoDown:
                    case EAttrType.AttrFreezeFrame:
                    case EAttrType.AttrShieldList:
                    case EAttrType.AttrPressingOpen:
                    case EAttrType.AttrUpLift:
                    default:
                        break;
                }
            }
        }

        var skillEffect = delta.SkillEffects;
        if (skillEffect?.Damages == null || skillEffect.Damages.Count == 0) return;

        var count = 0;
        var heals = 0;
        var crits = 0;

        foreach (var d in skillEffect.Damages)
        {
            var skillId = d.OwnerId;
            if (skillId == 0) continue;

            var attackerRaw = d.TopSummonerId != 0 ? d.TopSummonerId : d.AttackerUuid;
            if (attackerRaw == 0) continue;

            var isAttackerPlayer = attackerRaw.IsUuidPlayerRaw();
            var attackerUuid = attackerRaw.ShiftRight16();

            var damageSigned = d.HasValue ? d.Value : (d.HasLuckyValue ? d.LuckyValue : 0L);
            if (damageSigned == 0) continue;

            var (id, ticks) = IDGenerator.Next();
            _storage.AddBattleLog(new BattleLog
            {
                PacketID = id,
                TimeTicks = ticks,
                SkillID = skillId,
                AttackerUuid = attackerUuid,
                TargetUuid = targetUuid,
                Value = damageSigned,
                ValueElementType = (int)d.Property,
                DamageSourceType = (int)(d.HasDamageSource ? d.DamageSource : 0),
                IsAttackerPlayer = isAttackerPlayer,
                IsTargetPlayer = isTargetPlayer,
                IsLucky = d.HasLuckyValue && d.LuckyValue != 0,
                IsCritical = d.HasIsCrit && d.IsCrit,//(d.TypeFlag & 1) == 1,
                IsHeal = d.Type == EDamageType.Heal,
                IsMiss = d.HasIsMiss && d.IsMiss,
                IsDead = d.HasIsDead && d.IsDead
            });
            count++;
            if ((d.TypeFlag & 1) == 1) crits++;
            if (d.Type == EDamageType.Heal) heals++;
        }

        if (count > 0)
        {
            _logger?.LogTrace(CoreLogEvents.DeltaProcessed,
                "Delta processed: {Count} events (crit={Crit}, heal={Heal}) TargetPlayer={IsTargetPlayer}",
                count, crits, heals, isTargetPlayer);
        }
    }
}

public sealed class SyncToMeDeltaInfoProcessor(IDataStorage storage, ILogger? logger)
    : BaseDeltaInfoProcessor(storage, logger, WorldNtfMessageId.SyncToMeDeltaInfo)
{
    public override void Process(byte[] payload)
    {
        _logger?.LogDebug(CoreLogEvents.SyncToMeDelta, nameof(SyncToMeDeltaInfoProcessor));
        var syncToMeDeltaInfo = Zproto.WorldNtf.Types.SyncToMeDeltaInfo.Parser.ParseFrom(payload);
        var aoiSyncToMeDelta = syncToMeDeltaInfo.DeltaInfo;
        var uuid = aoiSyncToMeDelta.Uuid.ShiftRight16();
        if (uuid != 0 && _storage.CurrentPlayerUUID != uuid)
        {
            _storage.CurrentPlayerUUID = uuid;
        }

        var aoiSyncDelta = aoiSyncToMeDelta.BaseDelta;
        if (aoiSyncDelta == null) return;

        ProcessAoiSyncDelta(aoiSyncDelta);
    }
}

public sealed class SyncNearDeltaInfoProcessor(IDataStorage storage, ILogger? logger)
    : BaseDeltaInfoProcessor(storage, logger, WorldNtfMessageId.SyncNearDeltaInfo)
{
    public override void Process(byte[] payload)
    {
        _logger?.LogDebug(CoreLogEvents.SyncNearDelta, nameof(SyncNearDeltaInfoProcessor));
        var syncNearDeltaInfo = Zproto.WorldNtf.Types.SyncNearDeltaInfo.Parser.ParseFrom(payload);
        if (syncNearDeltaInfo.DeltaInfos == null || syncNearDeltaInfo.DeltaInfos.Count == 0) return;

        foreach (var aoiSyncDelta in syncNearDeltaInfo.DeltaInfos)
        {
            ProcessAoiSyncDelta(aoiSyncDelta);
        }
    }
}