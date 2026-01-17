using System;
using System.Collections.Generic;
using System.Text;
using PixelCrushers.DialogueSystem;
using Lavender.DialogueLib;
using Lavender;
using System.Linq;
using UnityEngine;
using static StorageModelController;

namespace MinitrainHobbyist
{
    internal class MikkelConversationPatcher : ConversationPatcher
    {
        const int ItemID_OsmoOlutMinitrain_Authentic = 17200;
        const int ItemID_OsmoOlutMinitrain_Replica = 400028; // Player needs to have a quality of at least 1.25 for Mikkel to accept it
        const int ItemGroupID_AllSellableMinis = 400012; // Every mini that Mikkel will buy belongs to this ItemGroup

        private bool isGivingReplica = false;
        private SlotController? selectedMiniSlot = null;
        private string selectedMiniToBuy = "";
        private float selectedMiniValue = 0;
        // TODO - Figure out how to setup a timer for Mikkel's purchasing to reset
        // I'll probably need to dynamically add a data storage actor to the world to get it to save, vis-a-vis Hustler prototype
        //  That can come later

        public MikkelConversationPatcher()
            : base("Mines/Monitoring/Mikkel Purola")
        {

        }

        protected override void PatchDialogue()
        {
            if (MinitrainHobbyistPlugin.Settings.CanFoolMikkelWithReplica.Value)
            {
                // Build Mikkel calling out the player for trying to give him a fake Osmo Olut mini mini
                DialogueEntry suspiciousMikkel = NPCSays($"Huh.{Pause} This model{Pause}.{Pause}.{Pause} Are you sure about this?");
                DialogueEntry suspiciousMikkel2 = NPCSays($"Look, I'm going to be honest with you.{Pause}{Pause} I don't think this is authentic.");
                DialogueEntry suspiciousMikkel3 = NPCSays($"You best take it back to wherever you got it, cause they sold you a fake.{Pause} Happens more often than you'd think.");
                Link(suspiciousMikkel, suspiciousMikkel2);
                Link(suspiciousMikkel2, suspiciousMikkel3);
                // Suspicious mikkel branch does not rejoin dialogue tree.  It ends here.

                // Update all the existing spots where we look for the Osmo Olut mini mini, and swap them to use our custom condition
                // Additionally, we add a similar looking node for when the player *doesn't* have the original or a sufficiently convincing replica to allow Mikkel to call them out
                List<DialogueEntry> OOMiniCheck = Conversation.dialogueEntries
                    .Where(de => !string.IsNullOrWhiteSpace(de.conditionsString) && de.conditionsString.Contains("CheckForItemDialogue(17200)"))
                    .ToList();
                foreach (DialogueEntry entry in OOMiniCheck)
                {
                    string originalCondition = entry.conditionsString;
                    entry.conditionsString = originalCondition.Replace("CheckForItemDialogue(17200)", "HasBribeTrain()");
                    DialogueEntry lookAtMyFake = PlayerSays(entry.DialogueText);
                    lookAtMyFake.conditionsString = originalCondition.Replace("CheckForItemDialogue(17200)", "HasInferiorBribeTrain()");
                    // Special handling for showing fake when first finding out about collection/bribe
                    if (lookAtMyFake.DialogueText.Contains("You mean this?"))
                    {
                        // This ensures that the player can ask Mikkel again about the speeding tickets and trigger the quest
                        lookAtMyFake.userScript = "Actor[\"Mines_Mikkel_Purola\"].Bribe = false";
                    }
                    Link(lookAtMyFake, suspiciousMikkel);

                    
                    // Find all dialogue entries that are pointing AT `entry`, so we can add a sibling for when the player has an inferior quality replica
                    List<DialogueEntry> DialoguesLeadingToOOMiniCheck = Conversation.dialogueEntries.Where(de =>
                        de.outgoingLinks != null && de.outgoingLinks.Any(l => l.destinationConversationID == Conversation.id && l.destinationDialogueID == entry.id)
                        ).ToList();

                    foreach (DialogueEntry src in DialoguesLeadingToOOMiniCheck)
                    {
                        Link(src, lookAtMyFake, LinkOrdering.AfterDialogue(entry));
                    }
                    
                }

                // Everywhere the Osmo Olut mini mini is removed, swap to our custom removal function that prioritizes removing a sufficiently convincing fake instead
                IEnumerable<DialogueEntry> OOMiniConsume = Conversation.dialogueEntries
                    .Where(de => !string.IsNullOrWhiteSpace(de.userScript) && de.userScript.Contains("RemoveItemDialogue(17200)"));
                foreach (DialogueEntry entry in OOMiniConsume)
                {
                    entry.userScript = entry.userScript.Replace("RemoveItemDialogue(17200)", "RemoveBribeTrain()");
                    entry.DialogueText = entry.DialogueText.Replace("(Give)", $"(Give {Lua("BribeTrainType()")})");
                }
            }


            if (MinitrainHobbyistPlugin.Settings.MikkelBuysMiniMinis.Value)
            {
                IEnumerable<DialogueEntry> standardIntros = GetResponsesTo(Conversation.GetFirstDialogueEntry())
                .Where(de => de.conditionsString.Contains("Start")) // Start our search from any initial response nodes that run off the "Start" criteria, which is how greetings are cycled between interactions
                .SelectMany(de => AdvanceToRespondable(de)) // Then progress down the dialog tree to find where we should actually put our player responses
                .Distinct(); // De-duplicate identical results

                DialogueEntry IGotMiniMinisForSale = PlayerSays("I've collected some minitrain models.  Interested?");
                // 400012 is category for all sellable mini minis
                // Only show the selling option after we've found out about Mikkel's minitrain collection AND completed the quest.
                // NOTE: Technically if we bribe Mikkel with RM we could not find out about the collection, but I'm gonna let that slide...
                IGotMiniMinisForSale.conditionsString = "Actor[\"Mines_Mikkel_Purola\"].SpeedCameraDisabled == true or Actor[\"Mines_Mikkel_Purola\"].SpeedCameraDoubled == true and HasSellableMini()";

                foreach (DialogueEntry entry in standardIntros)
                {
                    Link(entry, IGotMiniMinisForSale);
                }

                DialogueEntry MikkelPick = NPCSays($"Well, my collection is complete{Pause}, but...{Pause} it never hurts to have some rolling stock for playtime.");
                MikkelPick.userScript = "PickMiniMiniToBuy()";
                Link(IGotMiniMinisForSale, MikkelPick);

                DialogueEntry MikkelPick2 = NPCSays("Yeah, what've you got?");
                Link(MikkelPick, MikkelPick2);

                // TODO: Can I figure out how to fade to black and back here? Hmm
                DialogueEntry MikkelWants = NPCSays($"I'm interested in the {Lua("PreferredMiniName()")}. How's {Lua("PreferredMiniCost()")} RM? {Pause}That should be market value.");
                Link(MikkelPick2, MikkelWants);

                DialogueEntry Sell = PlayerSays("Deal");
                Sell.userScript = "BuyMiniMini()";
                Link(MikkelWants, Sell);

                DialogueEntry Thanks = NPCSays($"Always good to feed the minitrain addiction. {Pause}Thanks, miner.");
                Link(Sell, Thanks);

                DialogueEntry NoSell = PlayerSays("I'm going to have to pass.");
                Link(MikkelWants, NoSell);

                DialogueEntry MikkelNotSoldTo1 = NPCSays("I should have known.");
                DialogueEntry MikkelNotSoldTo2 = NPCSays("Just leave.");
                Link(NoSell, MikkelNotSoldTo1);
                Link(MikkelNotSoldTo1, MikkelNotSoldTo2);
            }
        }

        public override void OnConversationStarted(InteractableTalk interactableTalk)
        {
            base.OnConversationStarted(interactableTalk);
            PixelCrushers.DialogueSystem.Lua.RegisterFunction("HasBribeTrain", this, typeof(MikkelConversationPatcher).GetMethod("LuaHasBribeTrain"));
            PixelCrushers.DialogueSystem.Lua.RegisterFunction("RemoveBribeTrain", this, typeof(MikkelConversationPatcher).GetMethod("LuaRemoveBribeTrain"));
            PixelCrushers.DialogueSystem.Lua.RegisterFunction("HasInferiorBribeTrain", this, typeof(MikkelConversationPatcher).GetMethod("LuaHasInferiorBribeTrain"));
            PixelCrushers.DialogueSystem.Lua.RegisterFunction("BribeTrainType", this, typeof(MikkelConversationPatcher).GetMethod("LuaBribeTrainType"));

            PixelCrushers.DialogueSystem.Lua.RegisterFunction("PickMiniMiniToBuy", this, typeof(MikkelConversationPatcher).GetMethod("LuaPickMiniMiniToBuy"));
            PixelCrushers.DialogueSystem.Lua.RegisterFunction("BuyMiniMini", this, typeof(MikkelConversationPatcher).GetMethod("LuaBuyMiniMini"));
            PixelCrushers.DialogueSystem.Lua.RegisterFunction("PreferredMiniName", this, typeof(MikkelConversationPatcher).GetMethod("LuaPreferredMiniName"));
            PixelCrushers.DialogueSystem.Lua.RegisterFunction("PreferredMiniCost", this, typeof(MikkelConversationPatcher).GetMethod("LuaPreferredMiniCost"));
            PixelCrushers.DialogueSystem.Lua.RegisterFunction("HasSellableMini", this, typeof(MikkelConversationPatcher).GetMethod("LuaHasSellableMini"));
        }

        public override void OnConversationEnded(InteractableTalk interactableTalk)
        {
            base.OnConversationEnded(interactableTalk);
            PixelCrushers.DialogueSystem.Lua.UnregisterFunction("HasBribeTrain");
            PixelCrushers.DialogueSystem.Lua.UnregisterFunction("RemoveBribeTrain");
            PixelCrushers.DialogueSystem.Lua.UnregisterFunction("HasInferiorBribeTrain");
            PixelCrushers.DialogueSystem.Lua.UnregisterFunction("BribeTrainType");

            PixelCrushers.DialogueSystem.Lua.UnregisterFunction("PickMiniMiniToBuy");
            PixelCrushers.DialogueSystem.Lua.UnregisterFunction("BuyMiniMini");
            PixelCrushers.DialogueSystem.Lua.UnregisterFunction("PreferredMiniName");
            PixelCrushers.DialogueSystem.Lua.UnregisterFunction("PreferredMiniCost");
            PixelCrushers.DialogueSystem.Lua.UnregisterFunction("HasSellableMini");
        }

        public bool LuaHasBribeTrain()
        {
            bool bFound = false;
            isGivingReplica = false;

            foreach (SlotController slot in Inventory.instance.AllSlots(false, true))
            {
                if (slot.itemStack.itemAmount > 0)
                {
                    if (slot.itemStack.itemId == ItemID_OsmoOlutMinitrain_Authentic)
                    {
                        bFound = true; // Keep searching, we might have a high quality replica too
                    }
                    if (slot.itemStack.itemId == ItemID_OsmoOlutMinitrain_Replica)
                    {
                        OS.Items.ItemQualityData? itemQualityData = slot.itemStack.GetMetaOfType<OS.Items.ItemQualityData>();
                        if (itemQualityData != null &&
                            (itemQualityData.GetQuality() == OS.Items.ItemQualityData.Quality.Fine || itemQualityData.GetQuality() == OS.Items.ItemQualityData.Quality.Excellent))
                        {   // Mikkel is only fooled by quality > 1.5 (ie Fine, Excellent)
                            isGivingReplica = true;
                            return true;
                        }
                    }
                }
            }
            return bFound;
        }

        public bool LuaHasInferiorBribeTrain()
        {
            bool bFoundInferior = false;
            foreach (SlotController slot in Inventory.instance.AllSlots(false, true))
            {
                if (slot.itemStack.itemAmount > 0)
                {
                    if (slot.itemStack.itemId == ItemID_OsmoOlutMinitrain_Authentic)
                    {
                        // Has real train, don't even look for replica train
                        return false;
                    }

                    if (slot.itemStack.itemId == ItemID_OsmoOlutMinitrain_Replica)
                    {
                        OS.Items.ItemQualityData? itemQualityData = slot.itemStack.GetMetaOfType<OS.Items.ItemQualityData>();
                        if (itemQualityData == null ||
                            (itemQualityData.GetQuality() != OS.Items.ItemQualityData.Quality.Fine && itemQualityData.GetQuality() != OS.Items.ItemQualityData.Quality.Excellent))
                        {   // Mikkel is only fooled by quality > 1.5 (ie Fine, Excellent)
                            bFoundInferior = true;
                        }
                        else
                        {
                            // Has sufficient fake
                            return false;
                        }
                    }
                }
            }
            return bFoundInferior;
        }

        public void LuaRemoveBribeTrain()
        {
            SlotController? foundReplica = null;
            SlotController? foundAuthentic = null;
            foreach (SlotController slot in Inventory.instance.AllSlots(false, true))
            {
                if (slot.itemStack.itemAmount > 0)
                {
                    if (slot.itemStack.itemId == ItemID_OsmoOlutMinitrain_Authentic)
                    {
                        foundAuthentic = slot;
                    }
                    if (slot.itemStack.itemId == ItemID_OsmoOlutMinitrain_Replica)
                    {
                        OS.Items.ItemQualityData? itemQualityData = slot.itemStack.GetMetaOfType<OS.Items.ItemQualityData>();
                        if (itemQualityData != null &&
                            (itemQualityData.GetQuality() == OS.Items.ItemQualityData.Quality.Fine || itemQualityData.GetQuality() == OS.Items.ItemQualityData.Quality.Excellent))
                        {   // Mikkel is only fooled by quality > 1.5 (ie Fine, Excellent)
                            foundReplica = slot;
                        }
                    }
                }
            }

            if (foundReplica != null)
            {
                foundReplica.TakeItem(1);
            }
            else if (foundAuthentic != null)
            {
                foundAuthentic.TakeItem(1);
            }
        }

        public string LuaBribeTrainType()
        {
            return isGivingReplica ? "Convincing Replica" : "Original";
        }


        public bool LuaHasSellableMini()
        {
            foreach (SlotController slot in Inventory.instance.AllSlots(false, true))
            {
                if (slot.itemStack.itemAmount > 0)
                {
                    if (ItemOperations.GetItemAmountFromSlot(slot, ItemGroupID_AllSellableMinis, false, true, -1, false, true, false) > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void LuaPickMiniMiniToBuy()
        {
            List<SlotController> eligibleMinis = [];
            foreach (SlotController slot in Inventory.instance.AllSlots(false, true))
            {
                float numMatchingInSlot = ItemOperations.GetItemAmountFromSlot(slot, ItemGroupID_AllSellableMinis, false, true, -1, false, true, false);
                if (numMatchingInSlot > 0)
                {
                    eligibleMinis.Add(slot);
                }
            }

            if (eligibleMinis.Count == 0)
            {
                MinitrainHobbyistPlugin.Log.LogError($"MikkelConversationPatcher::LuaPickMiniMiniToBuy was executed, but there are no matching items in the inventory.");
                return;
            }

            selectedMiniSlot = eligibleMinis[UnityEngine.Random.Range(0, eligibleMinis.Count)];
            // This crazy chain of object accesses is how we go from "inventory slot" to "name of the item in that inventory slot"
            selectedMiniToBuy = selectedMiniSlot.itemStack.itemReference.cached.GetItemTitle(selectedMiniSlot.itemStack.Meta, false, false);
            selectedMiniValue = Mathf.Round(selectedMiniSlot.itemStack.itemReference.cached.GetItemSalePrice(selectedMiniSlot.itemStack.Meta) * 0.1f); // Mikkel pays in RM
        }

        public void LuaBuyMiniMini()
        {
            if (selectedMiniSlot != null && ItemOperations.GetItemAmountFromSlot(selectedMiniSlot, ItemGroupID_AllSellableMinis, false, true, -1, false, true, false) > 0)
            {
                selectedMiniSlot.TakeItem(1);
                Money.instance.AddMoneyDialogue(selectedMiniValue, true);
                selectedMiniSlot = null; // Make sure we don't somehow chain buy
            }
        }

        // Display the mini mini that Mikkel has decided he wants
        public string LuaPreferredMiniName()
        {
            return selectedMiniSlot != null ? selectedMiniToBuy : "Nothing";
        }

        // And how much he'll pay for it
        public float LuaPreferredMiniCost()
        {
            return selectedMiniSlot != null ? selectedMiniValue : 0;
        }
    }
}
