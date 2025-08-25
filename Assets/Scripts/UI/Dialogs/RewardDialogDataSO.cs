using System.Collections.Generic;
using UnityEngine;
using Data.Items;
using System;

/// <summary>
/// Abstract base class for reward dialog data and logic.
/// Subclasses must implement the reward delivery logic in DeliverRewards.
/// </summary>
public abstract class RewardDialogDataSO : ScriptableObject
{
    [Header("Dialog Title (optional)")]
    public string title;

    [Header("Dialog Description (optional)")]
    [TextArea]
    public string description;

    [Header("Reward Item IDs")]
    public List<string> rewardItemIds;

    [Header("Button Text (optional)")]
    public string buttonText = "OK";

    [Header("Selection Settings")]
    public bool allowSelection = false; // If true, user must select items
    public int minSelection = 1;        // Minimum items to select
    public int maxSelection = 1;        // Maximum items to select

    /// <summary>
    /// Called by the dialog controller when the user accepts the reward.
    /// Subclasses must implement the logic for delivering rewards.
    /// </summary>
    /// <param name="selectedItems">The list of selected ItemData (empty if not selectable)</param>
    /// <param name="hero">The hero receiving the rewards</param>
    public abstract void DeliverRewards(List<string> selectedItemsIds, HeroData hero);
}
