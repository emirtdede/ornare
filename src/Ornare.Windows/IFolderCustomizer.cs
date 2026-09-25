using Ornare.Core;

namespace Ornare.Windows;

public interface IFolderCustomizer
{
    bool IsSupportedPath(string folderPath);
    FolderCustomizationState GetFolderState(string folderPath, FolderBaseline? baseline);
    OperationResult<FolderIconSemantics> ApplyIcon(string folderPath, string stagedIcoPath, Action<JournalState>? onJournalStageChanged = null);
    OperationResult ResetIcon(string folderPath, FolderBaseline baseline, Action<JournalState>? onJournalStageChanged = null);
    OperationResult UndoIcon(string folderPath, FolderBaseline baseline, Action<JournalState>? onJournalStageChanged = null);
}
