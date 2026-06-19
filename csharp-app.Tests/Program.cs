using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using GameOfLife.Api;
using GameOfLife.Api.Models;
using GameOfLife.Api.Services;
using Microsoft.AspNetCore.Http;

string[] Block =
[
    "....",
    ".OO.",
    ".OO.",
    "...."
];

string[] BlinkerVertical =
[
    ".....",
    "..O..",
    "..O..",
    "..O..",
    "....."
];

string[] BlinkerHorizontal =
[
    ".....",
    ".....",
    ".OOO.",
    ".....",
    "....."
];

string[] Empty =
[
    "...",
    "...",
    "..."
];

string[] Glider =
[
    "....................",
    "..O.................",
    "...O................",
    ".OOO................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "...................."
];

string[] GliderAfterFour =
[
    "....................",
    "....................",
    "...O................",
    "....O...............",
    "..OOO...............",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "...................."
];

var tests = new (string Name, Func<Task> Run)[]
{
    ("Upload board returns board id", UploadBoardReturnsBoardId),
    ("Upload board returns Location header", UploadBoardReturnsLocationHeader),
    ("Uploaded board can be retrieved after service restart", UploadedBoardCanBeRetrievedAfterServiceRestart),
    ("Multiple boards persist independently", MultipleBoardsPersistIndependently),
    ("Stored board file content is valid JSON", StoredBoardFileContentIsValidJson),
    ("Corrupted board file is treated as not found", CorruptedBoardFileIsTreatedAsNotFound),
    ("Storage path override can be supplied", StoragePathOverrideCanBeSupplied),
    ("Temporary persistence files are not returned as boards", TemporaryPersistenceFilesAreNotReturnedAsBoards),
    ("Block pattern stays unchanged", BlockPatternStaysUnchanged),
    ("Blinker flips and flips back", BlinkerFlipsAndFlipsBack),
    ("Empty board remains empty", EmptyBoardRemainsEmpty),
    ("Get N states away returns expected result", GetNStatesAwayReturnsExpectedResult),
    ("Get states zero returns original board", GetStatesZeroReturnsOriginalBoard),
    ("Get next does not mutate stored board", GetNextDoesNotMutateStoredBoard),
    ("Repeated calls are deterministic", RepeatedCallsAreDeterministic),
    ("Underpopulation kills live cell", UnderpopulationKillsLiveCell),
    ("Overpopulation kills live cell", OverpopulationKillsLiveCell),
    ("Reproduction creates live cell", ReproductionCreatesLiveCell),
    ("Survival keeps live cell with two or three neighbors", SurvivalKeepsLiveCellWithTwoOrThreeNeighbors),
    ("Edge and corner neighbor counting is finite", EdgeAndCornerNeighborCountingIsFinite),
    ("Single-cell board becomes empty", SingleCellBoardBecomesEmpty),
    ("One-row board evolves correctly", OneRowBoardEvolvesCorrectly),
    ("One-column board evolves correctly", OneColumnBoardEvolvesCorrectly),
    ("Glider evolves as expected", GliderEvolvesAsExpected),
    ("Toad oscillator flips and flips back", ToadOscillatorFlipsAndFlipsBack),
    ("Beacon oscillator flips and flips back", BeaconOscillatorFlipsAndFlipsBack),
    ("Final state returns stable board for still life", FinalStateReturnsStableBoardForStillLife),
    ("Final state returns stable board after several generations", FinalStateReturnsStableBoardAfterSeveralGenerations),
    ("Final state returns error for oscillator", FinalStateReturnsErrorForOscillator),
    ("Final state detects oscillator before max attempts", FinalStateDetectsOscillatorBeforeMaxAttempts),
    ("Final state returns error for max-attempt exceeded", FinalStateReturnsErrorForMaxAttemptExceeded),
    ("Final state with zero attempts errors even for stable board", FinalStateWithZeroAttemptsErrorsEvenForStableBoard),
    ("Final-state search does not persist intermediate generations", FinalStateSearchDoesNotPersistIntermediateGenerations),
    ("Invalid board input returns validation error", InvalidBoardInputReturnsValidationError),
    ("Null rows returns validation error", NullRowsReturnsValidationError),
    ("Empty rows returns validation error", EmptyRowsReturnsValidationError),
    ("Empty row string returns validation error", EmptyRowStringReturnsValidationError),
    ("Invalid character returns validation error", InvalidCharacterReturnsValidationError),
    ("Alias cells normalize correctly", AliasCellsNormalizeCorrectly),
    ("Negative steps returns validation error", NegativeStepsReturnsValidationError),
    ("Negative maxAttempts returns validation error", NegativeMaxAttemptsReturnsValidationError),
    ("Steps over server limit returns validation error", StepsOverServerLimitReturnsValidationError),
    ("maxAttempts over server limit returns validation error", MaxAttemptsOverServerLimitReturnsValidationError),
    ("Board at max rows is accepted", BoardAtMaxRowsIsAccepted),
    ("Board at max columns is accepted", BoardAtMaxColumnsIsAccepted),
    ("Board at max total cells is accepted", BoardAtMaxTotalCellsIsAccepted),
    ("Board exceeding row limit returns validation error", BoardExceedingRowLimitReturnsValidationError),
    ("Board exceeding column limit returns validation error", BoardExceedingColumnLimitReturnsValidationError),
    ("Board exceeding total cell limit returns validation error", BoardExceedingTotalCellLimitReturnsValidationError),
    ("Large valid board computes one generation", LargeValidBoardComputesOneGeneration),
    ("High steps value is supported", HighStepsValueIsSupported),
    ("High maxAttempts value detects cycle", HighMaxAttemptsValueDetectsCycle),
    ("Unknown board id returns 404", UnknownBoardIdReturns404),
    ("Unknown board id returns 404 for next", UnknownBoardIdReturns404ForNext),
    ("Unknown board id returns 404 for N states", UnknownBoardIdReturns404ForNStates),
    ("Unknown board id returns 404 for final", UnknownBoardIdReturns404ForFinal),
    ("Response value has documented board schema", ResponseValueHasDocumentedBoardSchema),
    ("Running API accepts upload over real HTTP", RunningApiAcceptsUploadOverRealHttp),
    ("Running API returns validation error for malformed numeric query", RunningApiReturnsValidationErrorForMalformedNumericQuery),
    ("Running API returns validation error for malformed Content-Length", RunningApiReturnsValidationErrorForMalformedContentLength),
    ("Parallel uploads return unique ids", ParallelUploadsReturnUniqueIds),
    ("Parallel reads of same board are consistent", ParallelReadsOfSameBoardAreConsistent),
    ("Parallel uploads do not leave temp files", ParallelUploadsDoNotLeaveTempFiles)
};

var failures = new List<string>();

foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.Message}");
        Console.WriteLine($"FAIL {test.Name}");
        Console.WriteLine(ex);
    }
}

if (failures.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"{failures.Count} test(s) failed:");
    foreach (var failure in failures)
    {
        Console.WriteLine($"- {failure}");
    }

    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"{tests.Length} tests passed.");

async Task UploadBoardReturnsBoardId()
{
    var service = CreateService();

    var uploaded = await service.UploadAsync(new UploadBoardRequest(Block));

    Assert.NotEqual(Guid.Empty, uploaded.Id, "Uploaded board id should not be empty.");
    Assert.SequenceEqual(Block, uploaded.Rows, "Uploaded board should be returned unchanged.");
}

async Task UploadBoardReturnsLocationHeader()
{
    var service = CreateService();
    var result = await BoardEndpoints.Upload(new UploadBoardRequest(Block), service, CancellationToken.None);
    var uploaded = ExtractValue<BoardDto>(result);

    Assert.Equal(StatusCodes.Status201Created, await ExecuteAndGetStatusCode(result), "Upload should return HTTP 201.");
    Assert.Equal($"/boards/{uploaded.Id}", ExtractStringProperty(result, "Location"), "Upload should return a board Location.");
}

async Task UploadedBoardCanBeRetrievedAfterServiceRestart()
{
    var storagePath = CreateStoragePath();
    var firstService = CreateService(storagePath);
    var uploaded = await firstService.UploadAsync(new UploadBoardRequest(Block));

    var restartedService = CreateService(storagePath);
    var retrieved = await restartedService.GetAsync(uploaded.Id);

    Assert.NotNull(retrieved, "Board should be available from a new service instance.");
    Assert.SequenceEqual(Block, retrieved!.Rows, "Retrieved board should match uploaded board.");
}

async Task MultipleBoardsPersistIndependently()
{
    var storagePath = CreateStoragePath();
    var service = CreateService(storagePath);
    var block = await service.UploadAsync(new UploadBoardRequest(Block));
    var blinker = await service.UploadAsync(new UploadBoardRequest(BlinkerVertical));

    var restartedService = CreateService(storagePath);
    var retrievedBlock = await restartedService.GetAsync(block.Id);
    var retrievedBlinker = await restartedService.GetAsync(blinker.Id);

    Assert.SequenceEqual(Block, retrievedBlock!.Rows, "Block board should persist independently.");
    Assert.SequenceEqual(BlinkerVertical, retrievedBlinker!.Rows, "Blinker board should persist independently.");
}

async Task StoredBoardFileContentIsValidJson()
{
    var storagePath = CreateStoragePath();
    var service = CreateService(storagePath);
    var uploaded = await service.UploadAsync(new UploadBoardRequest(Block));
    var path = Path.Combine(storagePath, $"{uploaded.Id:N}.json");

    await using var stream = File.OpenRead(path);
    var stored = await JsonSerializer.DeserializeAsync<StoredBoard>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    Assert.NotNull(stored, "Stored board file should deserialize.");
    Assert.Equal(uploaded.Id, stored!.Id, "Stored file id should match uploaded id.");
    Assert.SequenceEqual(Block, stored.Rows, "Stored file rows should match uploaded rows.");
}

async Task CorruptedBoardFileIsTreatedAsNotFound()
{
    var storagePath = CreateStoragePath();
    Directory.CreateDirectory(storagePath);
    var id = Guid.NewGuid();
    await File.WriteAllTextAsync(Path.Combine(storagePath, $"{id:N}.json"), "{not-json");

    var retrieved = await CreateService(storagePath).GetAsync(id);

    Assert.Null(retrieved, "Corrupted board file should not crash reads.");
}

async Task StoragePathOverrideCanBeSupplied()
{
    var storagePath = CreateStoragePath();
    var service = CreateService(storagePath);
    var uploaded = await service.UploadAsync(new UploadBoardRequest(Block));

    Assert.True(File.Exists(Path.Combine(storagePath, $"{uploaded.Id:N}.json")), "Board should be saved in supplied storage path.");
}

async Task TemporaryPersistenceFilesAreNotReturnedAsBoards()
{
    var storagePath = CreateStoragePath();
    Directory.CreateDirectory(storagePath);
    var id = Guid.NewGuid();
    await File.WriteAllTextAsync(Path.Combine(storagePath, $"{id:N}.tmp"), "{}");

    var retrieved = await CreateService(storagePath).GetAsync(id);

    Assert.Null(retrieved, "Temporary files should not be visible as boards.");
}

async Task BlockPatternStaysUnchanged()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(Block));

    var next = await service.GetNextAsync(uploaded.Id);

    Assert.NotNull(next, "Next board should exist.");
    Assert.SequenceEqual(Block, next!.Rows, "Block still-life should not change.");
}

async Task BlinkerFlipsAndFlipsBack()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(BlinkerVertical));

    var next = await service.GetNextAsync(uploaded.Id);
    var twoSteps = await service.GetStateAfterAsync(uploaded.Id, 2);

    Assert.SequenceEqual(BlinkerHorizontal, next!.Rows, "Blinker should flip to horizontal.");
    Assert.SequenceEqual(BlinkerVertical, twoSteps!.Rows, "Blinker should flip back on the second generation.");
}

async Task EmptyBoardRemainsEmpty()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(Empty));

    var next = await service.GetNextAsync(uploaded.Id);

    Assert.SequenceEqual(Empty, next!.Rows, "Empty board should remain empty.");
}

async Task GetNStatesAwayReturnsExpectedResult()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(BlinkerVertical));

    var twoSteps = await service.GetStateAfterAsync(uploaded.Id, 2);

    Assert.Equal(2, twoSteps!.Generation, "Generation should match requested step count.");
    Assert.SequenceEqual(BlinkerVertical, twoSteps.Rows, "Two states from the blinker should match the original state.");
}

async Task GetStatesZeroReturnsOriginalBoard()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(BlinkerVertical));

    var zero = await service.GetStateAfterAsync(uploaded.Id, 0);

    Assert.Equal(0, zero!.Generation, "Zero-step result should be generation 0.");
    Assert.SequenceEqual(BlinkerVertical, zero.Rows, "Zero-step result should be the original board.");
}

async Task GetNextDoesNotMutateStoredBoard()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(BlinkerVertical));

    await service.GetNextAsync(uploaded.Id);
    var stored = await service.GetAsync(uploaded.Id);

    Assert.SequenceEqual(BlinkerVertical, stored!.Rows, "Next-state request should not mutate stored board.");
}

async Task RepeatedCallsAreDeterministic()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(Glider));

    var first = await service.GetStateAfterAsync(uploaded.Id, 4);
    var second = await service.GetStateAfterAsync(uploaded.Id, 4);

    Assert.SequenceEqual(first!.Rows, second!.Rows, "Repeated state requests should return the same board.");
}

Task UnderpopulationKillsLiveCell()
{
    var next = new GameOfLifeEngine().Next(["...", ".O.", "..."]);
    Assert.SequenceEqual(["...", "...", "..."], next, "Single live cell should die.");
    return Task.CompletedTask;
}

Task OverpopulationKillsLiveCell()
{
    var next = new GameOfLifeEngine().Next(["OOO", "OOO", "OOO"]);
    Assert.Equal('.', next[1][1], "Center cell with eight neighbors should die.");
    return Task.CompletedTask;
}

Task ReproductionCreatesLiveCell()
{
    var next = new GameOfLifeEngine().Next(["...", "OOO", "..."]);
    Assert.Equal('O', next[0][1], "Dead cell with three neighbors should become alive.");
    Assert.Equal('O', next[2][1], "Dead cell with three neighbors should become alive.");
    return Task.CompletedTask;
}

Task SurvivalKeepsLiveCellWithTwoOrThreeNeighbors()
{
    var twoNeighbors = new GameOfLifeEngine().Next(["OO.", "O..", "..."]);
    var threeNeighbors = new GameOfLifeEngine().Next(["OO.", "OO.", "..."]);

    Assert.Equal('O', twoNeighbors[0][0], "Live cell with two neighbors should survive.");
    Assert.Equal('O', threeNeighbors[0][0], "Live cell with three neighbors should survive.");
    return Task.CompletedTask;
}

Task EdgeAndCornerNeighborCountingIsFinite()
{
    var next = new GameOfLifeEngine().Next(["OO", "OO"]);
    Assert.SequenceEqual(["OO", "OO"], next, "Corner block should remain stable with finite-grid edges.");
    return Task.CompletedTask;
}

Task SingleCellBoardBecomesEmpty()
{
    var next = new GameOfLifeEngine().Next(["O"]);
    Assert.SequenceEqual(["."], next, "Single live cell should die.");
    return Task.CompletedTask;
}

Task OneRowBoardEvolvesCorrectly()
{
    var next = new GameOfLifeEngine().Next(["OOO"]);
    Assert.SequenceEqual([".O."], next, "One-row board should count only horizontal neighbors.");
    return Task.CompletedTask;
}

Task OneColumnBoardEvolvesCorrectly()
{
    var next = new GameOfLifeEngine().Next(["O", "O", "O"]);
    Assert.SequenceEqual([".", "O", "."], next, "One-column board should count only vertical neighbors.");
    return Task.CompletedTask;
}

Task GliderEvolvesAsExpected()
{
    var rows = (IReadOnlyList<string>)Glider;
    var engine = new GameOfLifeEngine();
    for (var i = 0; i < 4; i++)
    {
        rows = engine.Next(rows);
    }

    Assert.SequenceEqual(GliderAfterFour, rows, "Glider should move diagonally after four generations.");
    return Task.CompletedTask;
}

Task ToadOscillatorFlipsAndFlipsBack()
{
    var engine = new GameOfLifeEngine();
    string[] toad =
    [
        "......",
        "......",
        "..OOO.",
        ".OOO..",
        "......",
        "......"
    ];
    string[] expectedNext =
    [
        "......",
        "...O..",
        ".O..O.",
        ".O..O.",
        "..O...",
        "......"
    ];

    var next = engine.Next(toad);
    var back = engine.Next(next);

    Assert.SequenceEqual(expectedNext, next, "Toad should flip to its second phase.");
    Assert.SequenceEqual(toad, back, "Toad should flip back.");
    return Task.CompletedTask;
}

Task BeaconOscillatorFlipsAndFlipsBack()
{
    var engine = new GameOfLifeEngine();
    string[] beacon =
    [
        "......",
        ".OO...",
        ".OO...",
        "...OO.",
        "...OO.",
        "......"
    ];
    string[] expectedNext =
    [
        "......",
        ".OO...",
        ".O....",
        "....O.",
        "...OO.",
        "......"
    ];

    var next = engine.Next(beacon);
    var back = engine.Next(next);

    Assert.SequenceEqual(expectedNext, next, "Beacon should flip to its second phase.");
    Assert.SequenceEqual(beacon, back, "Beacon should flip back.");
    return Task.CompletedTask;
}

async Task FinalStateReturnsStableBoardForStillLife()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(Block));

    var final = await service.GetFinalStateAsync(uploaded.Id, maxAttempts: 5);

    Assert.Equal(FinalStateStatus.Found, final.Status, "Still-life should be found as final state.");
    Assert.SequenceEqual(Block, final.Board!.Rows, "Final state should be the stable block.");
}

async Task FinalStateReturnsStableBoardAfterSeveralGenerations()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(["OO", "O."]));

    var final = await service.GetFinalStateAsync(uploaded.Id, maxAttempts: 5);

    Assert.Equal(FinalStateStatus.Found, final.Status, "Board should stabilize into a block.");
    Assert.Equal(2, final.Board!.Generation, "Final-state detection should report the stabilizing generation.");
    Assert.SequenceEqual(["OO", "OO"], final.Board.Rows, "Board should stabilize into a 2x2 block.");
}

async Task FinalStateReturnsErrorForOscillator()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(BlinkerVertical));

    var final = await service.GetFinalStateAsync(uploaded.Id, maxAttempts: 10);

    Assert.Equal(FinalStateStatus.CycleDetected, final.Status, "Blinker should be detected as a cycle.");
    Assert.Contains("repeated cycle", final.Error, "Cycle error should explain why final state was not returned.");
}

async Task FinalStateDetectsOscillatorBeforeMaxAttempts()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(BlinkerVertical));

    var final = await service.GetFinalStateAsync(uploaded.Id, maxAttempts: 1_000);

    Assert.Equal(FinalStateStatus.CycleDetected, final.Status, "Cycle should be detected before exhausting max attempts.");
    Assert.Contains("generation 2", final.Error, "Blinker cycle should be detected at generation 2.");
}

async Task FinalStateReturnsErrorForMaxAttemptExceeded()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(BlinkerVertical));

    var final = await service.GetFinalStateAsync(uploaded.Id, maxAttempts: 0);

    Assert.Equal(FinalStateStatus.MaxAttemptsExceeded, final.Status, "Zero max attempts should fail before stabilization.");
    Assert.Contains("maxAttempts", final.Error, "Max attempt error should explain the limit.");
}

async Task FinalStateWithZeroAttemptsErrorsEvenForStableBoard()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(Block));

    var final = await service.GetFinalStateAsync(uploaded.Id, maxAttempts: 0);

    Assert.Equal(FinalStateStatus.MaxAttemptsExceeded, final.Status, "Zero attempts should not evaluate stability.");
}

async Task FinalStateSearchDoesNotPersistIntermediateGenerations()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(["OO", "O."]));

    await service.GetFinalStateAsync(uploaded.Id, maxAttempts: 5);
    var stored = await service.GetAsync(uploaded.Id);

    Assert.SequenceEqual(["OO", "O."], stored!.Rows, "Final-state search should not mutate stored board.");
}

async Task InvalidBoardInputReturnsValidationError()
{
    var service = CreateService();
    var result = await BoardEndpoints.Upload(new UploadBoardRequest(["OO", "O"]), service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status400BadRequest, await ExecuteAndGetStatusCode(result), "Invalid board should return HTTP 400.");
}

async Task NullRowsReturnsValidationError()
{
    var service = CreateService();
    var result = await BoardEndpoints.Upload(new UploadBoardRequest(null), service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status400BadRequest, await ExecuteAndGetStatusCode(result), "Null rows should return HTTP 400.");
}

async Task EmptyRowsReturnsValidationError()
{
    var service = CreateService();
    var result = await BoardEndpoints.Upload(new UploadBoardRequest([]), service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status400BadRequest, await ExecuteAndGetStatusCode(result), "Empty rows should return HTTP 400.");
}

async Task EmptyRowStringReturnsValidationError()
{
    var service = CreateService();
    var result = await BoardEndpoints.Upload(new UploadBoardRequest([""]), service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status400BadRequest, await ExecuteAndGetStatusCode(result), "Empty row string should return HTTP 400.");
}

async Task InvalidCharacterReturnsValidationError()
{
    var service = CreateService();
    var result = await BoardEndpoints.Upload(new UploadBoardRequest([".A."]), service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status400BadRequest, await ExecuteAndGetStatusCode(result), "Invalid characters should return HTTP 400.");
}

async Task AliasCellsNormalizeCorrectly()
{
    var service = CreateService();

    var uploaded = await service.UploadAsync(new UploadBoardRequest(["Xx*10."]));

    Assert.SequenceEqual(["OOOO.."], uploaded.Rows, "Alias cells should normalize to O and dot.");
}

async Task NegativeStepsReturnsValidationError()
{
    var service = CreateService();
    var result = await BoardEndpoints.GetStateAfter(Guid.NewGuid(), -1, service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status400BadRequest, await ExecuteAndGetStatusCode(result), "Negative steps should return HTTP 400.");
}

async Task NegativeMaxAttemptsReturnsValidationError()
{
    var service = CreateService();
    var result = await BoardEndpoints.GetFinal(Guid.NewGuid(), -1, service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status400BadRequest, await ExecuteAndGetStatusCode(result), "Negative maxAttempts should return HTTP 400.");
}

async Task StepsOverServerLimitReturnsValidationError()
{
    var service = CreateService();
    var result = await BoardEndpoints.GetStateAfter(Guid.NewGuid(), BoardService.MaxSteps + 1, service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status400BadRequest, await ExecuteAndGetStatusCode(result), "Steps over the server limit should return HTTP 400.");
}

async Task MaxAttemptsOverServerLimitReturnsValidationError()
{
    var service = CreateService();
    var result = await BoardEndpoints.GetFinal(Guid.NewGuid(), BoardService.MaxFinalStateAttempts + 1, service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status400BadRequest, await ExecuteAndGetStatusCode(result), "maxAttempts over the server limit should return HTTP 400.");
}

async Task BoardAtMaxRowsIsAccepted()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(Enumerable.Repeat(".", BoardValidator.MaxRows).ToArray()));

    Assert.Equal(BoardValidator.MaxRows, uploaded.Rows.Count, "Board at row limit should be accepted.");
}

async Task BoardAtMaxColumnsIsAccepted()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest([new string('.', BoardValidator.MaxColumns)]));

    Assert.Equal(BoardValidator.MaxColumns, uploaded.Rows[0].Length, "Board at column limit should be accepted.");
}

async Task BoardAtMaxTotalCellsIsAccepted()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(Enumerable.Repeat(new string('.', 1_000), 1_000).ToArray()));

    Assert.Equal(BoardValidator.MaxCells, uploaded.Rows.Count * uploaded.Rows[0].Length, "Board at total-cell limit should be accepted.");
}

async Task BoardExceedingRowLimitReturnsValidationError()
{
    var service = CreateService();
    var rows = Enumerable.Repeat(".", BoardValidator.MaxRows + 1).ToArray();
    var result = await BoardEndpoints.Upload(new UploadBoardRequest(rows), service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status400BadRequest, await ExecuteAndGetStatusCode(result), "Board exceeding row limit should return HTTP 400.");
}

async Task BoardExceedingColumnLimitReturnsValidationError()
{
    var service = CreateService();
    var rows = new[] { new string('.', BoardValidator.MaxColumns + 1) };
    var result = await BoardEndpoints.Upload(new UploadBoardRequest(rows), service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status400BadRequest, await ExecuteAndGetStatusCode(result), "Board exceeding column limit should return HTTP 400.");
}

async Task BoardExceedingTotalCellLimitReturnsValidationError()
{
    var service = CreateService();
    var rows = Enumerable.Repeat(new string('.', 1_000), 1_001).ToArray();
    var result = await BoardEndpoints.Upload(new UploadBoardRequest(rows), service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status400BadRequest, await ExecuteAndGetStatusCode(result), "Board exceeding total cell limit should return HTTP 400.");
}

async Task LargeValidBoardComputesOneGeneration()
{
    var service = CreateService();
    var rows = Enumerable.Repeat(new string('.', 200), 200).ToArray();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(rows));
    var stopwatch = Stopwatch.StartNew();

    var next = await service.GetNextAsync(uploaded.Id);

    stopwatch.Stop();
    Assert.SequenceEqual(rows, next!.Rows, "Large empty board should remain empty.");
    Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), "Large valid board should compute one generation in a reasonable time.");
}

async Task HighStepsValueIsSupported()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(BlinkerVertical));

    var result = await service.GetStateAfterAsync(uploaded.Id, 100);

    Assert.SequenceEqual(BlinkerVertical, result!.Rows, "Even high step count for blinker should return original phase.");
}

async Task HighMaxAttemptsValueDetectsCycle()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(BlinkerVertical));

    var result = await service.GetFinalStateAsync(uploaded.Id, 10_000);

    Assert.Equal(FinalStateStatus.CycleDetected, result.Status, "High maxAttempts should still short-circuit on cycle detection.");
}

async Task UnknownBoardIdReturns404()
{
    var service = CreateService();
    var result = await BoardEndpoints.Get(Guid.NewGuid(), service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status404NotFound, await ExecuteAndGetStatusCode(result), "Unknown board id should return HTTP 404.");
}

async Task UnknownBoardIdReturns404ForNext()
{
    var service = CreateService();
    var result = await BoardEndpoints.GetNext(Guid.NewGuid(), service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status404NotFound, await ExecuteAndGetStatusCode(result), "Unknown board id for next should return HTTP 404.");
}

async Task UnknownBoardIdReturns404ForNStates()
{
    var service = CreateService();
    var result = await BoardEndpoints.GetStateAfter(Guid.NewGuid(), 3, service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status404NotFound, await ExecuteAndGetStatusCode(result), "Unknown board id for N states should return HTTP 404.");
}

async Task UnknownBoardIdReturns404ForFinal()
{
    var service = CreateService();
    var result = await BoardEndpoints.GetFinal(Guid.NewGuid(), 3, service, CancellationToken.None);

    Assert.Equal(StatusCodes.Status404NotFound, await ExecuteAndGetStatusCode(result), "Unknown board id for final should return HTTP 404.");
}

async Task ResponseValueHasDocumentedBoardSchema()
{
    var service = CreateService();
    var result = await BoardEndpoints.Upload(new UploadBoardRequest(Block), service, CancellationToken.None);
    var value = ExtractValue<BoardDto>(result);

    Assert.NotEqual(Guid.Empty, value.Id, "Board response should expose id.");
    Assert.SequenceEqual(Block, value.Rows, "Board response should expose rows.");
    Assert.Equal(0, value.Generation, "Board response should expose generation.");
}

async Task RunningApiAcceptsUploadOverRealHttp()
{
    await using var api = await RunningApi.StartAsync();
    using var client = new HttpClient();
    using var content = new StringContent("{\"rows\":[\"....\",\".OO.\",\".OO.\",\"....\"]}", System.Text.Encoding.UTF8, "application/json");

    var response = await client.PostAsync($"{api.BaseUrl}/boards", content);
    var body = await response.Content.ReadAsStringAsync();
    var board = JsonSerializer.Deserialize<BoardDto>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    Assert.Equal(HttpStatusCode.Created, response.StatusCode, "Real HTTP upload should return 201.");
    Assert.NotNull(board, "Real HTTP upload should return a board response.");
    Assert.NotEqual(Guid.Empty, board!.Id, "Real HTTP upload should return a board id.");
    Assert.SequenceEqual(Block, board.Rows, "Real HTTP upload should return normalized rows.");
}

async Task RunningApiReturnsValidationErrorForMalformedNumericQuery()
{
    await using var api = await RunningApi.StartAsync();
    using var client = new HttpClient();

    var response = await client.GetAsync($"{api.BaseUrl}/boards/{Guid.NewGuid()}/final?maxAttempts=abc");

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode, "Malformed maxAttempts should return HTTP 400.");
}

async Task RunningApiReturnsValidationErrorForMalformedContentLength()
{
    await using var api = await RunningApi.StartAsync();

    var response = await SendRawHttpAsync(api.Port, "POST /boards HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: nope\r\n\r\n");

    Assert.Contains("400 Bad Request", response, "Malformed Content-Length should return HTTP 400.");
}

async Task ParallelUploadsReturnUniqueIds()
{
    var service = CreateService();
    var uploads = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => service.UploadAsync(new UploadBoardRequest(Block))));

    Assert.Equal(50, uploads.Select(upload => upload.Id).Distinct().Count(), "Parallel uploads should return unique ids.");
}

async Task ParallelReadsOfSameBoardAreConsistent()
{
    var service = CreateService();
    var uploaded = await service.UploadAsync(new UploadBoardRequest(Glider));

    var reads = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => service.GetStateAfterAsync(uploaded.Id, 4)));

    foreach (var read in reads)
    {
        Assert.SequenceEqual(GliderAfterFour, read!.Rows, "Parallel reads should be consistent.");
    }
}

async Task ParallelUploadsDoNotLeaveTempFiles()
{
    var storagePath = CreateStoragePath();
    var service = CreateService(storagePath);

    await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => service.UploadAsync(new UploadBoardRequest(Block))));

    Assert.Equal(0, Directory.EnumerateFiles(storagePath, "*.tmp").Count(), "Parallel uploads should not leave temp files.");
}

static BoardService CreateService(string? storagePath = null)
{
    return new BoardService(new FileBoardRepository(storagePath ?? CreateStoragePath()), new GameOfLifeEngine());
}

static string CreateStoragePath()
{
    return Path.Combine(Path.GetTempPath(), "game-of-life-tests", Guid.NewGuid().ToString("N"));
}

static async Task<string> SendRawHttpAsync(int port, string request)
{
    using var client = new TcpClient();
    await client.ConnectAsync(IPAddress.Loopback, port);
    await using var stream = client.GetStream();
    var bytes = System.Text.Encoding.ASCII.GetBytes(request);
    await stream.WriteAsync(bytes);

    using var reader = new StreamReader(stream, System.Text.Encoding.ASCII);
    return await reader.ReadToEndAsync();
}

static async Task<int> ExecuteAndGetStatusCode(IResult result)
{
    await Task.CompletedTask;

    if (result is IStatusCodeHttpResult statusCodeResult)
    {
        return statusCodeResult.StatusCode ?? StatusCodes.Status200OK;
    }

    throw new InvalidOperationException($"Result type '{result.GetType().Name}' does not expose a status code.");
}

static T ExtractValue<T>(IResult result)
{
    var value = result.GetType().GetProperty("Value")?.GetValue(result);
    if (value is T typed)
    {
        return typed;
    }

    throw new InvalidOperationException($"Result type '{result.GetType().Name}' did not expose value '{typeof(T).Name}'.");
}

static string? ExtractStringProperty(IResult result, string propertyName)
{
    return result.GetType().GetProperty(propertyName)?.GetValue(result) as string;
}

static class Assert
{
    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}. Actual: {actual}.");
        }
    }

    public static void NotEqual<T>(T notExpected, T actual, string message)
    {
        if (EqualityComparer<T>.Default.Equals(notExpected, actual))
        {
            throw new InvalidOperationException($"{message} Value: {actual}.");
        }
    }

    public static void NotNull<T>(T? actual, string message)
    {
        if (actual is null)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Null<T>(T? actual, string message)
    {
        if (actual is not null)
        {
            throw new InvalidOperationException($"{message} Actual: {actual}.");
        }
    }

    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Contains(string expected, string? actual, string message)
    {
        if (actual is null || !actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message} Expected to contain: {expected}. Actual: {actual ?? "<null>"}.");
        }
    }

    public static void SequenceEqual(IReadOnlyList<string> expected, IReadOnlyList<string> actual, string message)
    {
        if (expected.Count != actual.Count || !expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"{message}{Environment.NewLine}Expected:{Environment.NewLine}{string.Join(Environment.NewLine, expected)}{Environment.NewLine}Actual:{Environment.NewLine}{string.Join(Environment.NewLine, actual)}");
        }
    }
}

sealed class RunningApi : IAsyncDisposable
{
    private readonly Process _process;

    private RunningApi(Process process, int port, string storagePath)
    {
        _process = process;
        Port = port;
        StoragePath = storagePath;
        BaseUrl = $"http://127.0.0.1:{port}";
    }

    public int Port { get; }

    public string StoragePath { get; }

    public string BaseUrl { get; }

    public static async Task<RunningApi> StartAsync()
    {
        var port = GetFreePort();
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var apiDll = Path.Combine(root, "csharp-app", "bin", "Debug", "net10.0", "csharp-app.dll");
        var storagePath = Path.Combine(Path.GetTempPath(), "game-of-life-http-tests", Guid.NewGuid().ToString("N"));

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{apiDll}\" --urls http://127.0.0.1:{port}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment["GAME_OF_LIFE_STORAGE"] = storagePath;

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start API process.");
        var api = new RunningApi(process, port, storagePath);

        await api.WaitUntilReadyAsync();
        return api;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process.Dispose();
    }

    private async Task WaitUntilReadyAsync()
    {
        using var client = new HttpClient();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        var url = $"{BaseUrl}/boards/{Guid.Empty}";

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                var stdout = await _process.StandardOutput.ReadToEndAsync();
                var stderr = await _process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"API process exited before listening. stdout: {stdout} stderr: {stderr}");
            }

            try
            {
                using var response = await client.GetAsync(url);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("API process did not start listening within 10 seconds.");
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
