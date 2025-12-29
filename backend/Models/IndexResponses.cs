namespace ChatBot.Models;

public record IndexStatsResponse(uint Count, bool HasRecords);

public record IndexBuildResponse(string Message, int RecordCount);
