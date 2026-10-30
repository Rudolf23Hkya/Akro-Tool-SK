using System;
using System.Collections.Generic;
using System.Windows.Media;
using Atletika_SutaznyPlan_Generator.Models;

namespace Atletika_SutaznyPlan_Generator.ViewModels
{
    // Used BOTH for:
    // - main slots / Okno (SlotIndex + Image)
    // - exercise picker cells (X,Y + ImagePath)
    public class ExerciseCardVm : ViewModelBase
    {
        private ImageSource? _image;
        public ImageSource? Image
        {
            get => _image;
            set => SetProperty(ref _image, value);
        }

        private string _label = "";
        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        private bool _isSelectable = true;
        public bool IsSelectable
        {
            get => _isSelectable;
            set => SetProperty(ref _isSelectable, value);
        }

        private bool _isBlockedByRowRule;
        public bool IsBlockedByRowRule
        {
            get => _isBlockedByRowRule;
            set => SetProperty(ref _isBlockedByRowRule, value);
        }

        // Picker cell without an exercise in the rulebook (shown empty, never selectable)
        public bool IsEmptyCell { get; set; }

        // ===== Zvlášť (one exercise per team member, issue #29) — main slots only =====

        // Empty Okno in a team age group while an Inv pick is left: show the "Zvlášť" button (issue #43)
        private bool _canSeparate;
        public bool CanSeparate
        {
            get => _canSeparate;
            set => SetProperty(ref _canSeparate, value);
        }

        // Okno holds one exercise per team member: show arrows + performer overlay
        private bool _isSeparate;
        public bool IsSeparate
        {
            get => _isSeparate;
            set => SetProperty(ref _isSeparate, value);
        }

        // Who performs the exercise shown right now, e.g. "Ján Novák (Base)" or "1 (Base)"
        private string _performerText = "";
        public string PerformerText
        {
            get => _performerText;
            set => SetProperty(ref _performerText, value);
        }

        // Pictures of the Okno in member order and the one currently shown
        public IReadOnlyList<ImageSource?> SeparateImages { get; set; } = Array.Empty<ImageSource?>();
        public int ShownExerciseIndex { get; set; }

        // ===== Backend metadata for a selected exercise cell =====
        public string? ImagePath { get; set; } // absolute path from ExerciseImageRepository
        public int X { get; set; }             // column (1-based)
        public int Y { get; set; }             // row (1-based)
        public Rulebook Rulebook { get; set; }
        public Category Category { get; set; }

        // ===== Main window slot metadata =====
        public int SlotIndex { get; set; }     // 0..SlotCount-1
    }
}