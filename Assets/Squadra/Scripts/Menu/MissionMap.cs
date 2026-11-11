using System;
using System.Collections.Generic;
using Squadra.Campaign;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// The campaign map: the catalog's missions as a row of connected dots, a page at a time. Each dot
    /// shows whether its mission is completed, available, locked or only a preview, and an available one
    /// launches its mission. When a mission has just been unlocked the map opens on its page and plays
    /// the unlock once.
    /// </summary>
    public sealed class MissionMap : MonoBehaviour
    {
        const string LitConnectorClass = "mission__connector--lit";
        const string UnlockingClass = "mission--unlocking";
        const string DenseClass = "mission-slots--dense";

        // Above this many dots per page the slots get narrow, so the dots and labels shrink.
        const int DenseFrom = 7;

        // The unlock reveal, in milliseconds.
        const long RevealDelay = 450;
        const long ConnectorTime = 550;
        const long PulseTime = 500;

        static readonly string[] StateClasses =
        {
            "mission--preview", "mission--locked", "mission--available", "mission--completed",
        };

        [SerializeField] MissionCatalog catalog;
        [Tooltip("How many missions one map page shows. More missions than this adds page arrows.")]
        [Range(MissionPages.MinPerPage, MissionPages.MaxPerPage)]
        [SerializeField] int missionsPerPage = MissionPages.DefaultPerPage;

        sealed class Slot
        {
            public Button Root;
            public VisualElement Connector;
            public MenuIcon Icon;
            public Label Name;
            public Label Status;
            public int Mission = -1;
        }

        readonly List<Slot> slots = new List<Slot>();
        readonly List<IVisualElementScheduledItem> reveals = new List<IVisualElementScheduledItem>();

        VisualElement slotRow;
        Button previousButton;
        Button nextButton;
        MenuAudio sounds;
        Action<int> launch;
        MissionSlot[] missions = Array.Empty<MissionSlot>();
        MissionPages pages;
        int page;
        bool open;

        public MissionCatalog Catalog => catalog;
        public int MissionsPerPage => missionsPerPage;
        public int Page => page;
        public int PageCount => pages.PageCount;

        /// <summary>Connects the map to its screen. <paramref name="onLaunch"/> gets the catalog index to play.</summary>
        public void Bind(VisualElement screen, MenuAudio audio, Action<int> onLaunch)
        {
            sounds = audio;
            launch = onLaunch;
            slotRow = screen.Q("missionSlots");
            previousButton = screen.Q<Button>("previousPageButton");
            nextButton = screen.Q<Button>("nextPageButton");
            previousButton.clicked += () => PreviousPage();
            nextButton.clicked += () => NextPage();

            // Left and right turn pages here, so they must not also move the focus between dots.
            screen.RegisterCallback<NavigationMoveEvent>(move =>
            {
                if (move.direction != NavigationMoveEvent.Direction.Left
                    && move.direction != NavigationMoveEvent.Direction.Right) return;

                move.StopPropagation();
                screen.focusController?.IgnoreEvent(move);
            }, TrickleDown.TrickleDown);

            Refresh();
        }

        /// <summary>Replaces the catalog and page size at run time, for example to try a longer campaign.</summary>
        public void Configure(MissionCatalog newCatalog, int perPage)
        {
            catalog = newCatalog;
            missionsPerPage = Mathf.Clamp(perPage, MissionPages.MinPerPage, MissionPages.MaxPerPage);
            page = 0;
            if (slotRow != null) Refresh();
        }

        /// <summary>
        /// Call when the map screen appears. Shows the page of a newly unlocked mission and plays its
        /// reveal, or otherwise the page of the next mission to play.
        /// </summary>
        public void Open()
        {
            open = true;
            ReadCatalog();
            CampaignProgress progress = CampaignSession.Progress;
            int focus = CampaignRules.FirstPendingReveal(missions, progress);
            if (focus < 0) focus = CampaignRules.NextMission(missions, progress);
            page = focus < 0 ? 0 : pages.PageOf(focus);
            Render();
        }

        /// <summary>Call when the map screen is left. A reveal that was still playing stays pending.</summary>
        public void Close()
        {
            open = false;
            CancelReveals();
        }

        /// <summary>Same as clicking the previous-page arrow. Returns false when already on the first page.</summary>
        public bool PreviousPage() => TurnPage(-1);

        /// <summary>Same as clicking the next-page arrow. Returns false when already on the last page.</summary>
        public bool NextPage() => TurnPage(1);

        /// <summary>The element that should get keyboard focus when the map opens.</summary>
        public Focusable DefaultFocus()
        {
            Slot fallback = null;
            foreach (Slot slot in slots)
            {
                if (slot.Mission < 0 || !slot.Root.enabledSelf) continue;
                if (CampaignRules.StateOf(missions, CampaignSession.Progress, slot.Mission) == MissionState.Available)
                    return slot.Root;
                fallback ??= slot;
            }

            return fallback?.Root;
        }

        // ------------------------------------------------------------------ pages

        bool TurnPage(int direction)
        {
            int target = page + direction;
            if (target < 0 || target >= pages.PageCount) return false;

            page = target;
            if (sounds != null) sounds.Select();
            Render();
            KeepFocus(direction);
            return true;
        }

        // A page turn can lock or empty the dot that had the keyboard focus, or disable the arrow that
        // was just used at the last page. Focus then moves to a dot that can be played, or failing that
        // to the arrow that still leads somewhere.
        void KeepFocus(int direction)
        {
            var focused = slotRow.focusController?.focusedElement as VisualElement;
            if (focused != null && focused.enabledInHierarchy && !focused.ClassListContains("mission")) return;

            Focusable target = DefaultFocus();
            if (target == null)
            {
                Button onward = direction > 0 ? nextButton : previousButton;
                target = onward.enabledSelf ? onward : direction > 0 ? previousButton : nextButton;
            }

            if (target.canGrabFocus) target.Focus();
        }

        void ReadCatalog()
        {
            missions = catalog != null ? catalog.ToSlots() : Array.Empty<MissionSlot>();
            pages = new MissionPages(missions.Length, missionsPerPage);
            page = pages.ClampPage(page);
        }

        void Refresh()
        {
            ReadCatalog();
            Render();
        }

        void Render()
        {
            CancelReveals();
            BuildSlots();

            CampaignProgress progress = CampaignSession.Progress;
            int first = pages.FirstOnPage(page);
            int count = pages.CountOnPage(page);
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                bool used = i < count;
                slot.Mission = used ? first + i : -1;
                slot.Root.EnableInClassList("mission--empty", !used);
                if (!used)
                {
                    slot.Root.SetEnabled(false);
                    slot.Name.text = string.Empty;
                    slot.Status.text = string.Empty;
                    continue;
                }

                int index = first + i;
                bool pending = CampaignRules.NeedsReveal(missions, progress, index);
                Show(slot, index, pending ? MissionState.Locked : CampaignRules.StateOf(missions, progress, index));

                // The line to the next dot is lit once that mission is open to play.
                bool linked = i + 1 < count;
                slot.Connector.EnableInClassList("hidden", !linked);
                slot.Connector.EnableInClassList(LitConnectorClass, linked
                    && CampaignRules.CanLaunch(missions, progress, index + 1)
                    && !CampaignRules.NeedsReveal(missions, progress, index + 1));

                if (pending && open) Reveal(i);
            }

            bool arrows = pages.HasSeveralPages;
            previousButton.EnableInClassList("hidden", !arrows);
            nextButton.EnableInClassList("hidden", !arrows);
            previousButton.SetEnabled(pages.HasPrevious(page));
            nextButton.SetEnabled(pages.HasNext(page));
        }

        // ------------------------------------------------------------------ slots

        void BuildSlots()
        {
            if (slots.Count == pages.PerPage) return;

            slotRow.Clear();
            slots.Clear();
            slotRow.EnableInClassList(DenseClass, pages.PerPage >= DenseFrom);
            for (int i = 0; i < pages.PerPage; i++)
            {
                var slot = new Slot { Root = new Button() };
                slot.Root.AddToClassList("mission");

                // First, so the dots (its own and the next slot's) are drawn over the line.
                slot.Connector = new VisualElement { pickingMode = PickingMode.Ignore };
                slot.Connector.AddToClassList("mission__connector");
                var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.AddToClassList("mission__connector-fill");
                slot.Connector.Add(fill);
                slot.Root.Add(slot.Connector);

                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList("mission__dot");
                var core = new VisualElement { pickingMode = PickingMode.Ignore };
                core.AddToClassList("mission__core");
                dot.Add(core);
                slot.Icon = new MenuIcon();
                slot.Icon.AddToClassList("mission__icon");
                dot.Add(slot.Icon);
                slot.Root.Add(dot);

                slot.Name = new Label { pickingMode = PickingMode.Ignore };
                slot.Name.AddToClassList("mission__name");
                slot.Root.Add(slot.Name);
                slot.Status = new Label { pickingMode = PickingMode.Ignore };
                slot.Status.AddToClassList("mission__status");
                slot.Root.Add(slot.Status);

                slot.Root.clicked += () => Activate(slot);
                if (sounds != null) sounds.AttachHover(slot.Root);
                slotRow.Add(slot.Root);
                slots.Add(slot);
            }
        }

        void Show(Slot slot, int index, MissionState state)
        {
            for (int i = 0; i < StateClasses.Length; i++)
                slot.Root.EnableInClassList(StateClasses[i], i == (int)state);
            slot.Root.RemoveFromClassList(UnlockingClass);
            slot.Root.SetEnabled(state == MissionState.Available || state == MissionState.Completed);

            slot.Name.text = MissionCatalog.LabelFor(index).ToUpperInvariant();
            slot.Icon.Kind = state == MissionState.Completed ? MenuIconKind.Check : MenuIconKind.Lock;
            switch (state)
            {
                case MissionState.Completed:
                    slot.Status.text = "COMPLETED";
                    break;
                case MissionState.Available:
                    slot.Status.text = string.Empty; // the lit dot says it; only the other states are labeled
                    break;
                case MissionState.Locked:
                    slot.Status.text = "LOCKED\nComplete " + MissionCatalog.LabelFor(index - 1);
                    break;
                default:
                    slot.Status.text = "COMING SOON";
                    break;
            }
        }

        void Activate(Slot slot)
        {
            if (slot.Mission < 0 || !CampaignRules.CanLaunch(missions, CampaignSession.Progress, slot.Mission)) return;
            launch?.Invoke(slot.Mission);
        }

        // ------------------------------------------------------------------ unlock reveal

        // The dot starts out locked; the line from the mission before it lights up (when that mission is
        // on this page too), then the lock gives way with a pulse. Only then is the reveal acknowledged.
        void Reveal(int slotIndex)
        {
            Slot slot = slots[slotIndex];
            int mission = slot.Mission;
            long unlockAt = RevealDelay;
            if (slotIndex > 0)
            {
                VisualElement connector = slots[slotIndex - 1].Connector;
                Schedule(slot, RevealDelay, () => connector.AddToClassList(LitConnectorClass));
                unlockAt += ConnectorTime;
            }

            Schedule(slot, unlockAt, () =>
            {
                Show(slot, mission, MissionState.Available);
                slot.Root.AddToClassList(UnlockingClass);
            });
            Schedule(slot, unlockAt + PulseTime, () =>
            {
                slot.Root.RemoveFromClassList(UnlockingClass);
                CampaignSession.Progress.MarkRevealed(missions[mission].Id);
                slot.Root.Focus();
            });
        }

        void Schedule(Slot slot, long delay, Action action)
        {
            reveals.Add(slot.Root.schedule.Execute(action).StartingIn(delay));
        }

        void CancelReveals()
        {
            foreach (IVisualElementScheduledItem reveal in reveals)
                reveal.Pause();
            reveals.Clear();
        }

#if UNITY_EDITOR
        // Lets the page size be tuned in the Inspector while the game runs.
        void OnValidate()
        {
            missionsPerPage = Mathf.Clamp(missionsPerPage, MissionPages.MinPerPage, MissionPages.MaxPerPage);
            if (Application.isPlaying && slotRow != null) Refresh();
        }
#endif
    }
}
