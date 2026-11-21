using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>명시적으로 실행하는 모델 및 Play Mode 통합 검사입니다.</summary>
public static class SlidingPuzzleValidation
{
    private const string RunningKey = "SlidingPuzzle.ValidationRunning";
    private const string ReportPath = "Temp/SlidingPuzzleValidation.txt";
    private static readonly StringBuilder Report = new StringBuilder();
    private static IEnumerator _routine;
    private static int _completionCount;
    private static bool _runtimeError;

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (File.Exists("Temp/SlidingPuzzle.Build.request"))
            EditorApplication.delayCall += RunRequestedValidation;
    }

    private static void RunRequestedValidation()
    {
        if (!File.Exists("Temp/SlidingPuzzle.Build.request"))
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunRequestedValidation;
            return;
        }
        File.Delete("Temp/SlidingPuzzle.Build.request");
        BuildAndValidate();
    }

    [MenuItem("Tools/Sliding Puzzle/Create And Run Play Mode Checks")]
    public static void BuildAndValidate()
    {
        try
        {
            if (!SlidingPuzzleTestSceneBuilder.TryCreateScene())
                return;
            Report.Clear();
            ValidateModel();
            File.WriteAllText(ReportPath, Report.ToString());
            SessionState.SetBool(RunningKey, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    [MenuItem("Tools/Sliding Puzzle/Validate Model")]
    public static void ValidateModel()
    {
        SlidingPuzzleModel model = new SlidingPuzzleModel();
        Check(model.IsSolved, "Initial solved state");
        Check(model.TryApplyState(new[] { 1, 2, 3, 4, 0, 6, 7, 5, 8 }), "Apply valid state");
        Check(!model.TryMove(1) && !model.TryMove(0) && !model.TryMove(9), "Reject diagonal, blank and unknown tile");
        Check(model.TryMove(5) && model.TryMove(8) && model.IsSolved, "Legal moves and completion");
        Check(!model.TryMove(8), "Completed board is locked");
        Check(model.TryApplyState(new[] { 1, 2, 0, 4, 5, 3, 7, 8, 6 }), "Apply row boundary state");
        Check(!model.TryMove(4), "Reject wrapped row adjacency");

        int[] before = model.GetStateCopy();
        Check(!model.TryApplyState(null) && !model.TryApplyState(new[] { 0 }), "Reject null and wrong size");
        Check(!model.TryApplyState(new[] { 1, 2, 3, 4, 5, 6, 7, 7, 0 }), "Reject duplicates");
        Check(!model.TryApplyState(new[] { 1, 2, 3, 4, 5, 6, 7, 9, 0 }), "Reject invalid IDs");
        Check(!model.TryApplyState(new[] { 1, 2, 3, 4, 5, 6, 8, 7, 0 }), "Reject unsolvable parity");
        Check(Equal(before, model.GetStateCopy()), "Rejected states preserve existing state");
        int[] exported = model.GetStateCopy();
        exported[0] = 99;
        Check(model.GetTileAt(0) == before[0], "Export is a defensive copy");
        model.TryApplyState(before);
        before[0] = 99;
        Check(model.GetTileAt(0) == 1, "Import is a defensive copy");
        Dictionary<string, int> distances = BuildDistanceLookup();
        int[][] candidates = ((Lazy<int[][]>)typeof(SlidingPuzzleModel).GetField(
            "_startingStates", System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.NonPublic).GetValue(null)).Value;
        Check(candidates.Length == 554, "All 554 starting states at distance 8-10 are available");
        foreach (int[] candidate in candidates)
        {
            Check(distances.TryGetValue(string.Concat(candidate), out int distance) &&
                distance >= 8 && distance <= 10, "Candidate has exact minimum distance 8-10", false);
        }
        for (int seed = 0; seed < 100; seed++)
        {
            model.Shuffle(new System.Random(seed));
            Check(!model.IsSolved && new SlidingPuzzleModel().TryApplyState(model.GetStateCopy()),
                "Solvable non-complete shuffle seed " + seed, false);
            Check(distances.TryGetValue(string.Concat(model.GetStateCopy()), out int distance) &&
                distance >= 8 && distance <= 10, "Generated board minimum distance 8-10", false);
        }
        Check(true, "100 deterministic shuffles are valid, solvable and non-complete");
        Debug.Log("[SlidingPuzzle] Model validation passed.");
    }

    // 모델의 숫자 인코딩과 별개로 문자열 배치를 사용해 최소 거리를 검증합니다.
    private static Dictionary<string, int> BuildDistanceLookup()
    {
        Dictionary<string, int> distances = new Dictionary<string, int>();
        Queue<string> pending = new Queue<string>();
        distances.Add("123456780", 0);
        pending.Enqueue("123456780");
        int[] offsets = { -3, 3, -1, 1 };
        while (pending.Count > 0)
        {
            string state = pending.Dequeue();
            int distance = distances[state];
            if (distance == 10)
                continue;
            int empty = state.IndexOf('0');
            foreach (int offset in offsets)
            {
                int next = empty + offset;
                if (next < 0 || next >= 9 ||
                    (offset == -1 && empty % 3 == 0) || (offset == 1 && empty % 3 == 2))
                    continue;
                char[] cells = state.ToCharArray();
                cells[empty] = cells[next];
                cells[next] = '0';
                string key = new string(cells);
                if (distances.ContainsKey(key))
                    continue;
                distances.Add(key, distance + 1);
                pending.Enqueue(key);
            }
        }
        return distances;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(RunningKey, false) || change != PlayModeStateChange.EnteredPlayMode)
            return;
        Report.Clear();
        _completionCount = 0;
        _runtimeError = false;
        Report.Append(File.ReadAllText(ReportPath));
        Application.logMessageReceived += OnLog;
        _routine = ValidateRuntime();
        EditorApplication.update += Step;
    }

    private static void OnLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            _runtimeError = true;
            Report.AppendLine("RUNTIME ERROR: " + message);
        }
    }

    private static void Step()
    {
        try
        {
            if (_routine.MoveNext())
                return;
            Check(!_runtimeError, "No runtime errors");
            Report.AppendLine("PASS: All model and Play Mode checks completed.");
            Finish(0);
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static IEnumerator ValidateRuntime()
    {
        yield return null;
        LocalSlidingPuzzleController first = GameObject.Find("Puzzle A State").GetComponent<LocalSlidingPuzzleController>();
        LocalSlidingPuzzleController second = GameObject.Find("Puzzle B State").GetComponent<LocalSlidingPuzzleController>();
        int[] secondInitial = second.GetStateCopy();
        first.Completed += OnCompleted;
        first.Open();
        SlidingPuzzleUI ui = GameObject.Find("Puzzle A UI").GetComponent<SlidingPuzzleUI>();
        Transform board = ui.transform.Find("Board");
        Button tile7 = board.Find("Tile 7").GetComponent<Button>();
        Button tile8 = board.Find("Tile 8").GetComponent<Button>();
        Button close = ui.transform.Find("Close").GetComponent<Button>();
        Check(ui.isActiveAndEnabled, "Interaction entry point opens puzzle A");
        Check(first.TryApplyState(new[] { 1, 2, 3, 4, 5, 6, 0, 7, 8 }), "Apply test state to active UI");
        Vector2 start = ((RectTransform)tile7.transform).anchoredPosition;
        Click(tile7);
        Check(ui.IsAnimating && Equal(first.GetStateCopy(), new[] { 1, 2, 3, 4, 5, 6, 7, 0, 8 }),
            "Click commits model before animation completes");
        Click(tile8);
        Check(!first.IsSolved, "Further clicks are blocked while moving");
        Click(close);
        Check(!ui.gameObject.activeSelf && !ui.IsAnimating, "Closing cancels motion");
        second.Open();
        Check(Equal(secondInitial, second.GetStateCopy()), "Puzzle B keeps independent state");
        second.Close();
        first.Open();
        Check(Equal(first.GetStateCopy(), new[] { 1, 2, 3, 4, 5, 6, 7, 0, 8 }), "Reopening preserves committed move");
        Check(((RectTransform)tile7.transform).anchoredPosition.x < start.x, "Reopening snaps tile to committed destination");
        Check(!GameObject.Find("Puzzle B State").GetComponent<LocalSlidingPuzzleController>().IsSolved,
            "Switching puzzle does not solve other board");

        Time.timeScale = 0f;
        Click(tile8);
        double deadline = EditorApplication.timeSinceStartup + 0.5;
        while (EditorApplication.timeSinceStartup < deadline)
            yield return null;
        Check(first.IsSolved && !ui.IsAnimating && !tile8.interactable,
            "Unscaled movement finishes and completed board locks");
        Time.timeScale = 1f;
        Click(tile8);
        Check(_completionCount == 1, "Completion event fires once per solved transition");

        first.TryApplyState(new[] { 1, 2, 3, 4, 5, 6, 0, 7, 8 });
        Click(tile7);
        int[] replacement = { 1, 2, 3, 4, 0, 6, 7, 5, 8 };
        Check(first.TryApplyState(replacement) && !ui.IsAnimating, "External snapshot interrupts old animation");
        Check(Equal(replacement, first.GetStateCopy()), "External snapshot replaces state");
        first.NewPuzzle();
        Check(!first.IsSolved && !ui.IsAnimating && tile8.interactable,
            "New puzzle resets completion and motion");
        for (int i = 0; i < 5; i++)
        {
            Click(close);
            first.Open();
        }
        first.TryApplyState(new[] { 1, 2, 3, 4, 5, 6, 7, 0, 8 });
        Click(tile8);
        deadline = EditorApplication.timeSinceStartup + 0.5;
        while (EditorApplication.timeSinceStartup < deadline)
            yield return null;
        Check(_completionCount == 2, "Repeated reopen does not duplicate event handlers");

        first.TryApplyState(new[] { 1, 2, 3, 4, 0, 6, 7, 5, 8 });
        Canvas.ForceUpdateCanvases();
        CapturePreview();
        first.Completed -= OnCompleted;
    }

    private static void CapturePreview()
    {
        Camera camera = GameObject.Find("Preview Camera").GetComponent<Camera>();
        RenderTexture target = RenderTexture.GetTemporary(1280, 800, 24);
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        Texture2D image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
        image.Apply();
        File.WriteAllBytes("Temp/SlidingPuzzlePreview.png", image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        UnityEngine.Object.Destroy(image);
        Check(true, "Saved rendered UI preview");
    }

    private static void Click(Button button)
    {
        PointerEventData pointer = new PointerEventData(EventSystem.current);
        pointer.button = PointerEventData.InputButton.Left;
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static void OnCompleted()
    {
        _completionCount++;
    }

    private static bool Equal(int[] left, int[] right)
    {
        if (left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
                return false;
        }
        return true;
    }

    private static void Check(bool condition, string description, bool record = true)
    {
        if (!condition)
            throw new InvalidOperationException(description);
        if (record)
            Report.AppendLine("PASS: " + description);
    }

    private static void Fail(Exception exception)
    {
        Report.AppendLine("FAIL: " + exception);
        Debug.LogException(exception);
        Finish(1);
    }

    private static void Finish(int code)
    {
        File.WriteAllText(ReportPath, Report.ToString());
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Step;
        Application.logMessageReceived -= OnLog;
        if (Application.isBatchMode)
            EditorApplication.Exit(code);
        else
            EditorApplication.ExitPlaymode();
    }
}
