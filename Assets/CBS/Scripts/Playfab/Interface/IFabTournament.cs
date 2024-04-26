using PlayFab;
using PlayFab.CloudScriptModels;
using System;

public interface IFabTournament
{
    void GetTournamentState(string profileID, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);
    void FindAndJoinTournament(string profileID, string playerEntityID, Action<ExecuteFunctionResult> onJoin, Action<PlayFabError> onFailed);
    void LeaveTournament(string profileID, string playerEntityID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
    void UpdateTournamentPoint(string profileID, int point, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);
    void AddTournamentPoint(string profileID, int point, Action<ExecuteFunctionResult> onUpdate, Action<PlayFabError> onFailed);
    void FinishTournament(string profileID, string playerEntityID, Action<ExecuteFunctionResult> onFinish, Action<PlayFabError> onFailed);
    void GetTournamentDataByID(string tournamentID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
    void GetAllTournaments(Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);
}
