using Tower.Core;

namespace Tower.Infrastructure;

public static class ControllerPerformance
{
    // Record once per session, including after restoring an older save of that session.
    public static PerformanceReward? Record(ControllerProfile profile, Session session, int randomPercent)
    {
        if (session.Options.Practice || session.ResultsRecorded || profile.RecordedSessions.Contains(session.Id))
        {
            return null;
        }
        var previous = profile.AverageRating;
        var hadRatedSession = profile.Sessions > 0;
        var rating = session.Performance.Rating(session);
        if (!profile.AirportRatings.TryGetValue(session.Airport.Code, out var ratings))
        {
            ratings = [];
            profile.AirportRatings[session.Airport.Code] = ratings;
        }
        ratings.Add(rating);
        if (ratings.Count > 10)
        {
            ratings.RemoveRange(0, ratings.Count - 10);
        }
        profile.RecordedSessions.Add(session.Id);
        profile.Sessions++;
        profile.Score += rating;
        profile.Arrivals += session.Performance.Arrivals;
        profile.Departures += session.Performance.Departures;
        profile.Incidents += session.Performance.Incidents;
        var reward = hadRatedSession
            ? PerformanceRewards.Choose(previous, profile.AverageRating, session.Performance.HasOperationalErrors ? 1 : 0, randomPercent)
            : null;
        if (reward is not null)
        {
            profile.RewardCounts[reward.Kind] = profile.RewardCounts.GetValueOrDefault(reward.Kind) + 1;
        }
        return reward;
    }
}
