using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StarResonanceDpsAnalysis.Core.Data;

namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;

/// <summary>
/// A registry that maps message method IDs to their corresponding processors.
/// </summary>
internal sealed class WorldNtfMessageHandlerRegistry
{
    private readonly Dictionary<WorldNtfMessageId, WorldNtfBaseProcessor> _processors;

    public WorldNtfMessageHandlerRegistry(IDataStorage storage, ILogger? logger)
    {
        var emptyLogger = logger ?? NullLogger.Instance;
        _processors = new Dictionary<WorldNtfMessageId, WorldNtfBaseProcessor>
        {
            { WorldNtfMessageId.SyncNearEntities, new SyncNearEntitiesProcessor(storage, logger) },
            { WorldNtfMessageId.SyncPioneerInfo, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.SyncPioneerInfo) },
            { WorldNtfMessageId.SyncSwitchChange, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.SyncSwitchChange) },
            { WorldNtfMessageId.SyncSwitchInfo, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.SyncSwitchInfo) },
            { WorldNtfMessageId.EnterGame, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.EnterGame) },
            { WorldNtfMessageId.SyncContainerData, new SyncContainerDataProcessor(storage, logger) },
            { WorldNtfMessageId.SyncContainerDirtyData, new SyncContainerDirtyDataProcessor(storage, logger) },
            { WorldNtfMessageId.SyncDungeonData, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.SyncDungeonData) },
            { WorldNtfMessageId.AwardNotify, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.AwardNotify) },
            { WorldNtfMessageId.CardInfoAck, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.CardInfoAck) },
            { WorldNtfMessageId.SyncSeason, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.SyncSeason) },
            { WorldNtfMessageId.UserAction, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.UserAction) },
            {
                WorldNtfMessageId.NotifyDisplayPlayHelp,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyDisplayPlayHelp)
            },
            {
                WorldNtfMessageId.NotifyApplicationInteraction,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyApplicationInteraction)
            },
            { WorldNtfMessageId.NotifyIsAgree, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyIsAgree) },
            {
                WorldNtfMessageId.NotifyCancelAction,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyCancelAction)
            },
            {
                WorldNtfMessageId.NotifyUploadPictureResult,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyUploadPictureResult)
            },
            { WorldNtfMessageId.SyncInvite, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.SyncInvite) },
            {
                WorldNtfMessageId.NotifyRedDotChange,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyRedDotChange)
            },
            {
                WorldNtfMessageId.ChangeNameResultNtf,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.ChangeNameResultNtf)
            },
            { WorldNtfMessageId.NotifyReviveUser, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyReviveUser) },
            {
                WorldNtfMessageId.NotifyParkourRankInfo,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyParkourRankInfo)
            },
            {
                WorldNtfMessageId.NotifyParkourRecordInfo,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyParkourRecordInfo)
            },
            { WorldNtfMessageId.NotifyShowTips, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyShowTips) },
            { WorldNtfMessageId.NotifyNoticeInfo, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyNoticeInfo) },
            { WorldNtfMessageId.SyncNearDeltaInfo, new SyncNearDeltaInfoProcessor(storage, logger) },
            { WorldNtfMessageId.SyncToMeDeltaInfo, new SyncToMeDeltaInfoProcessor(storage, logger) },
            {
                WorldNtfMessageId.NotifyClientKickOff,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyClientKickOff)
            },
            { WorldNtfMessageId.PaymentResponse, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.PaymentResponse) },
            {
                WorldNtfMessageId.NotifyUnlockCookBook,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyUnlockCookBook)
            },
            {
                WorldNtfMessageId.NotifyCustomEvent,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyCustomEvent)
            },
            {
                WorldNtfMessageId.NotifyStartPlayingDungeon,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyStartPlayingDungeon)
            },
            {
                WorldNtfMessageId.ChangeShowIdResultNtf,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.ChangeShowIdResultNtf)
            },
            { WorldNtfMessageId.NotifyShowItems, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyShowItems) },
            {
                WorldNtfMessageId.NotifySeasonActivationTargetInfo,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifySeasonActivationTargetInfo)
            },
            {
                WorldNtfMessageId.NotifyTextCheckResult,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyTextCheckResult)
            },
            {
                WorldNtfMessageId.NotifyDebugMessageTip,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyDebugMessageTip)
            },
            {
                WorldNtfMessageId.NotifyUserCloseFunction,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyUserCloseFunction)
            },
            {
                WorldNtfMessageId.NotifyServerCloseFunction,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyServerCloseFunction)
            },
            {
                WorldNtfMessageId.NotifyAwardAllItems,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyAwardAllItems)
            },
            {
                WorldNtfMessageId.NotifyAllMemberReady,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyAllMemberReady)
            },
            {
                WorldNtfMessageId.NotifyCaptainReady,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyCaptainReady)
            },
            {
                WorldNtfMessageId.NotifyUserAllSourcePrivilegeEffectData,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyUserAllSourcePrivilegeEffectData)
            },
            {
                WorldNtfMessageId.NotifyQuestAccept,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyQuestAccept)
            },
            {
                WorldNtfMessageId.NotifyQuestChangeStep,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyQuestChangeStep)
            },
            {
                WorldNtfMessageId.NotifyQuestGiveUp,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyQuestGiveUp)
            },
            {
                WorldNtfMessageId.NotifyQuestComplete,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyQuestComplete)
            },
            {
                WorldNtfMessageId.NotifyUserAllValidBattlePassData,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyUserAllValidBattlePassData)
            },
            {
                WorldNtfMessageId.NotifyNoticeMultiLanguageInfo,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyNoticeMultiLanguageInfo)
            },
            { WorldNtfMessageId.QteBegin, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.QteBegin) },
            { WorldNtfMessageId.QuestAbort, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.QuestAbort) },
            {
                WorldNtfMessageId.NotifyBuyShopResult,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyBuyShopResult)
            },
            {
                WorldNtfMessageId.NotifyShopItemCanBuy,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyShopItemCanBuy)
            },
            {
                WorldNtfMessageId.WorldBossRankInfoNtf,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.WorldBossRankInfoNtf)
            },
            {
                WorldNtfMessageId.EnterMatchResultNtf,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.EnterMatchResultNtf)
            },
            {
                WorldNtfMessageId.NotifyDriverApplyRide,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyDriverApplyRide)
            },
            {
                WorldNtfMessageId.NotifyInviteApplyRide,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyInviteApplyRide)
            },
            {
                WorldNtfMessageId.NotifyRideIsAgree,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyRideIsAgree)
            },
            { WorldNtfMessageId.NotifyPayInfo, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyPayInfo) },
            {
                WorldNtfMessageId.NotifyLifeProfessionWorkHistoryChange,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyLifeProfessionWorkHistoryChange)
            },
            {
                WorldNtfMessageId.NotifyLifeProfessionUnlockRecipe,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyLifeProfessionUnlockRecipe)
            },
            { WorldNtfMessageId.SignRewardNotify, new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.SignRewardNotify) },
            {
                WorldNtfMessageId.NotifyEntryRandomData,
                new WorldNtfEmptyProcessor(emptyLogger, WorldNtfMessageId.NotifyEntryRandomData)
            },
            {
                WorldNtfMessageId.SyncServerTime,
                new SyncServerTimeProcessor()
            },
            {
                WorldNtfMessageId.SyncServerSkillEnd,
                new SyncServerSkillEndProcessor()
            }
        };
    }

    /// <summary>
    /// Tries to get the processor for a given method ID.
    /// </summary>
    /// <param name="methodId">The method ID of the message.</param>
    /// <param name="processor">The resolved processor, if found.</param>
    /// <returns>True if a processor was found, otherwise false.</returns>
    public bool TryGetProcessor(uint methodId, [NotNullWhen(true)] out IMessageProcessor? processor)
    {
        var method = (WorldNtfMessageId)methodId;
        if (Enum.IsDefined(typeof(WorldNtfMessageId), method))
        {
            if (_processors.TryGetValue(method, out var ret))
            {
                processor = ret;
                return true;
            }

            Debug.WriteLine($"No processor registered for method: {method} ({methodId})");
            processor = null;
            return false;
        }

        Debug.WriteLine($"No processor found for method ID: {methodId}");
        processor = null;
        return false;
    }

    /// <inheritdoc cref="TryGetProcessor(uint,out StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.IMessageProcessor?)"/>
    public bool TryGetProcessor(WorldNtfMessageId id, [NotNullWhen(true)] out IMessageProcessor? processor)
    {
        var result = _processors.TryGetValue(id, out var ret);
        processor = ret;
        return result;
    }
}