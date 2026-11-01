using System.Collections.Generic;
using System.Linq;

namespace EdrLocal;

public static class StateCopies
{
	private static AppState WithoutHistory(AppState s)
	{
		AppState appState = new AppState();
		appState.Version = s.Version;
		appState.StationTrackOccupations = s.StationTrackOccupations;
		appState.Stations = s.Stations;
		appState.Users = s.Users;
		appState.Trains = s.Trains;
		appState.Entries = s.Entries;
		appState.Duties = s.Duties;
		appState.History = new List<Audit>();
		appState.Services = s.Services;
		appState.Layout = s.Layout;
		appState.UiZoom = s.UiZoom;
		return appState;
	}

	public static AppState Transaction(AppState s)
	{
		AppState appState = Json.Clone(WithoutHistory(s));
		appState.History = s.History.Select((Audit a) => new Audit
		{
			Time = a.Time,
			User = a.User,
			UserName = a.UserName,
			Action = a.Action,
			Entity = a.Entity,
			Before = a.Before,
			After = a.After
		}).ToList();
		return appState;
	}

	public static AppState Audit(AppState s)
	{
		AppState appState = WithoutHistory(s);
		appState.Users = s.Users.Select((LocalUser u) => new LocalUser
		{
			Id = u.Id,
			Name = u.Name,
			Role = u.Role,
			Salt = "[ukryte]",
			Hash = "[ukryte]"
		}).ToList();
		return appState;
	}
}
