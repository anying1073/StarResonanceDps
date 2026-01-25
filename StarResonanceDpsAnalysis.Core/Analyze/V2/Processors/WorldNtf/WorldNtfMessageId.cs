namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;

public enum WorldNtfMessageId : uint
{
    SyncNearEntities = 0x00000006U,
    SyncPioneerInfo = 0x0000000EU,
    SyncSwitchChange = 0x00000012U,
    SyncSwitchInfo = 0x00000013U,
    EnterGame = 0x00000014U,
    SyncContainerData = 0x00000015U,
    SyncContainerDirtyData = 0x00000016U,
    SyncDungeonData = 0x00000017U,
    AwardNotify = 0x00000019U,
    CardInfoAck = 0x0000001AU,
    SyncSeason = 0x0000001BU,
    UserAction = 0x0000001CU,
    NotifyDisplayPlayHelp = 0x0000001DU,
    NotifyApplicationInteraction = 0x0000001EU,
    NotifyIsAgree = 0x0000001FU,
    NotifyCancelAction = 0x00000020U,
    NotifyUploadPictureResult = 0x00000021U,
    SyncInvite = 0x00000024U,
    NotifyRedDotChange = 0x00000025U,
    ChangeNameResultNtf = 0x00000026U,
    NotifyReviveUser = 0x00000027U,
    NotifyParkourRankInfo = 0x00000028U,
    NotifyParkourRecordInfo = 0x00000029U,
    NotifyShowTips = 0x0000002AU,
    SyncServerTime = 0x0000002BU,
    NotifyNoticeInfo = 0x0000002CU,
    SyncNearDeltaInfo = 45U,
    SyncToMeDeltaInfo = 46U,
    NotifyClientKickOff = 0x00000031U,
    PaymentResponse = 0x00000033U,
    NotifyUnlockCookBook = 0x00000035U,
    NotifyCustomEvent = 0x00000036U,
    NotifyStartPlayingDungeon = 0x00000037U,
    ChangeShowIdResultNtf = 0x00000038U,
    NotifyShowItems = 0x00000039U,
    NotifySeasonActivationTargetInfo = 0x0000003AU,
    NotifyTextCheckResult = 0x0000003BU,
    NotifyDebugMessageTip = 0x0000003DU,
    NotifyUserCloseFunction = 0x0000003EU,
    NotifyServerCloseFunction = 0x0000003FU,
    NotifyAwardAllItems = 0x00000045U,
    NotifyAllMemberReady = 0x00000046U,
    NotifyCaptainReady = 0x00000047U,
    NotifyUserAllSourcePrivilegeEffectData = 0x0000004AU,
    NotifyQuestAccept = 0x0000004BU,
    NotifyQuestChangeStep = 0x0000004CU,
    NotifyQuestGiveUp = 0x0000004DU,
    NotifyQuestComplete = 0x0000004EU,
    NotifyUserAllValidBattlePassData = 0x0000004FU,
    NotifyNoticeMultiLanguageInfo = 0x00000053U,
    QteBegin = 0x00003001U,
    SyncClientUseSkill = 12290,
    NotifyBuffChange = 12291,
    SyncServerSkillStageEnd = 0x3004,
    SyncServerSkillEnd = 12293U,
    QuestAbort = 0x00006001U,
    NotifyBuyShopResult = 0x00029001U,
    NotifyShopItemCanBuy = 0x00029002U,
    WorldBossRankInfoNtf = 0x00046001U,
    EnterMatchResultNtf = 0x00048001U,
    NotifyDriverApplyRide = 0x0004D001U,
    NotifyInviteApplyRide = 0x0004D002U,
    NotifyRideIsAgree = 0x0004D003U,
    NotifyPayInfo = 0x00051001U,
    NotifyLifeProfessionWorkHistoryChange = 0x00052001U,
    NotifyLifeProfessionUnlockRecipe = 0x00052002U,
    SignRewardNotify = 0x0005E001U,
    NotifyEntryRandomData = 0x0006B001U
}

public static class MessageMethodExtensions
{
    public static bool TryParseMessageMethod(string methodName, out WorldNtfMessageId id)
    {
        return Enum.TryParse(methodName, ignoreCase: true, out id);
    }

    public static uint ToUInt32(this WorldNtfMessageId id)
    {
        return (uint)id;
    }

    public static WorldNtfMessageId FromUInt32(uint value)
    {
        return (WorldNtfMessageId)value;
    }
}