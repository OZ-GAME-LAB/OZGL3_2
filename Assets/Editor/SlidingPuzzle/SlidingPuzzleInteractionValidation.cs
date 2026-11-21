using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>실제 호스트, 상호작용 컨트롤러와 Addressables UI를 사용하는 명시적 검사입니다.</summary>
public static class SlidingPuzzleInteractionValidation
{
    private const string SessionKey = "SlidingPuzzle.InteractionValidation";
    private const string Request = ".utmp/SlidingPuzzle.Interaction.request";
    private const string Report = ".utmp/sliding-puzzle-validation.txt";
    private static IEnumerator _routine;
    private static bool _failed;

    [Serializable]
    private sealed class SceneBackup { public SceneSetup[] Scenes; }

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (File.Exists(Request))
            EditorApplication.delayCall += RunRequested;
    }

    private static void RunRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunRequested;
            return;
        }
        if (!File.Exists(Request))
            return;
        File.Delete(Request);
        Run();
    }

    [MenuItem("Tools/Sliding Puzzle/Validate Plate Interaction")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode 실행 중에는 검사를 시작하지 않습니다.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("저장되지 않은 씬이 있어 검사를 중단합니다.");
        SessionState.SetString(SessionKey + ".Scenes", JsonUtility.ToJson(new SceneBackup
            { Scenes = EditorSceneManager.GetSceneManagerSetup() }));
        File.WriteAllText(Report, "Sliding puzzle interaction Play Mode checks\n");
        EditorSceneManager.OpenScene(PlayerInteractionTestSceneBuilder.ScenePath);
        SessionState.SetBool(SessionKey, true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(SessionKey, false))
            return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _failed = false;
            Application.logMessageReceived += OnLog;
            _routine = Validate();
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            Application.logMessageReceived -= OnLog;
            SessionState.SetBool(SessionKey, false);
            SceneBackup backup = JsonUtility.FromJson<SceneBackup>(SessionState.GetString(SessionKey + ".Scenes", ""));
            if (backup != null)
                EditorSceneManager.RestoreSceneManagerSetup(backup.Scenes);
        }
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return;
        _failed = true;
        File.AppendAllText(Report, "FAIL runtime: " + message + "\n");
    }

    private static void Tick()
    {
        try
        {
            if (_routine.MoveNext())
                return;
        }
        catch (Exception exception)
        {
            _failed = true;
            File.AppendAllText(Report, "FAIL " + exception + "\n");
        }
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        EditorApplication.update -= Tick;
        File.AppendAllText(Report, _failed ? "FAIL completed\n" : "PASS completed\n");
        EditorApplication.ExitPlaymode();
    }

    private static IEnumerator Validate()
    {
        PlayerInteractionTestBootstrap bootstrap = null;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            bootstrap = root.GetComponentInChildren<PlayerInteractionTestBootstrap>();
            if (bootstrap != null) break;
        }
        float deadline = Time.realtimeSinceStartup + 25f;
        while (bootstrap != null && !bootstrap.IsReady && Time.realtimeSinceStartup < deadline)
            yield return null;
        Check(bootstrap != null && bootstrap.IsReady, "host and owner player ready");
        PlayerControllerView player = bootstrap.Player;
        PlayerInteractionController interaction = player.GetComponent<PlayerInteractionController>();
        PlayerInput input = player.GetComponent<PlayerInput>();
        GameObject plate = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SlidingPuzzleInteractionSetup.PlatePath));
        SlidingPuzzleInteractable puzzle = plate.GetComponent<SlidingPuzzleInteractable>();
        Collider2D area = plate.GetComponent<Collider2D>();
        GameObject marker = Get<GameObject>(puzzle, "_activateOnComplete");
        plate.transform.position += player.transform.position - area.bounds.center;
        Physics2D.SyncTransforms();
        Check(!marker.activeSelf && area.enabled && area.isTrigger, "initial marker hidden and trigger enabled");
        deadline = Time.realtimeSinceStartup + 5f;
        while (interaction.SelectedTarget != puzzle && Time.realtimeSinceStartup < deadline) yield return null;
        Check(interaction.SelectedTarget == puzzle, "existing trigger detects plate");

        // 최초 Addressables 로딩 중 취소와 재요청을 검사합니다.
        Check(interaction.TryRequestInteraction(), "begin first load");
        interaction.CancelRequestedInteraction();
        Check(input.currentActionMap.name == "Player", "cancel during loading restores input");
        Check(interaction.TryRequestInteraction(), "reopen while previous load settles");
        deadline = Time.realtimeSinceStartup + 15f;
        while (Get<SlidingPuzzleUI>(puzzle, "_ui") == null && Time.realtimeSinceStartup < deadline) yield return null;
        SlidingPuzzleUI ui = Get<SlidingPuzzleUI>(puzzle, "_ui");
        Check(ui != null && UIManager.Inst.IsOpen(UIType.SlidingPuzzleUI), "one cached UI opens after load cancellation");
        Check(input.currentActionMap.name == "MiniGame", "puzzle uses existing mini-game input map");
        Check(UIManager.Inst.HasDialogueConflict(), "dialogue sees puzzle UI conflict");
        SlidingPuzzleModel model = Get<SlidingPuzzleModel>(puzzle, "_model");
        int[] saved = model.GetStateCopy();
        interaction.CancelRequestedInteraction();
        Check(!UIManager.Inst.IsOpen(UIType.SlidingPuzzleUI) && input.currentActionMap.name == "Player", "cancel closes UI and restores player");
        Check(area.enabled && !marker.activeSelf && !puzzle.IsSolved, "cancel does not complete plate");

        // 기존 F 입력 바인딩으로 다시 열고, 실제 UI 타일 버튼으로 해결합니다.
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.F));
        deadline = Time.realtimeSinceStartup + 5f;
        while (!UIManager.Inst.IsOpen(UIType.SlidingPuzzleUI) && Time.realtimeSinceStartup < deadline) yield return null;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        Check(UIManager.Inst.IsOpen(UIType.SlidingPuzzleUI), "F opens cached puzzle");
        Check(Equal(saved, model.GetStateCopy()), "reopening preserves board");
        ui = Get<SlidingPuzzleUI>(puzzle, "_ui");
        foreach (int tileId in Solve(saved))
        {
            foreach (SlidingPuzzleTileUI tile in ui.GetComponentsInChildren<SlidingPuzzleTileUI>())
                if (tile.TileId == tileId) tile.GetComponent<Button>().onClick.Invoke();
            deadline = Time.realtimeSinceStartup + 3f;
            while (ui.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            Check(!ui.IsAnimating, "tile animation finished");
        }
        deadline = Time.realtimeSinceStartup + 3f;
        while (UIManager.Inst.IsOpen(UIType.SlidingPuzzleUI) && Time.realtimeSinceStartup < deadline) yield return null;
        Check(puzzle.IsSolved && marker.activeSelf, "success activates AncientPlate_Activate");
        Check(!area.enabled && plate.activeSelf, "success disables trigger but preserves plate");
        Check(!UIManager.Inst.IsOpen(UIType.SlidingPuzzleUI) && input.currentActionMap.name == "Player", "success closes UI and restores player input");
        Check(!puzzle.ViewModel.IsAvailable && !interaction.TryRequestInteraction(), "success prevents F interaction");
        plate.SetActive(false);
        plate.SetActive(true);
        Check(!area.enabled && marker.activeSelf && !puzzle.ViewModel.IsAvailable, "reactivation preserves local completion");
    }

    private static T Get<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void Check(bool passed, string name)
    {
        File.AppendAllText(Report, (passed ? "PASS " : "FAIL ") + name + "\n");
        if (!passed) throw new InvalidOperationException(name);
    }

    private static bool Equal(int[] a, int[] b)
    {
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private static int[] Solve(int[] start)
    {
        Queue<(int[] cells, int[] moves)> queue = new Queue<(int[], int[])>();
        HashSet<string> visited = new HashSet<string>();
        queue.Enqueue((start, Array.Empty<int>()));
        visited.Add(string.Join(",", start));
        while (queue.Count > 0)
        {
            var item = queue.Dequeue();
            SlidingPuzzleModel model = new SlidingPuzzleModel();
            model.TryApplyState(item.cells);
            if (model.IsSolved) return item.moves;
            for (int tile = 1; tile < SlidingPuzzleModel.CellCount; tile++)
            {
                model.TryApplyState(item.cells);
                if (!model.TryMove(tile)) continue;
                int[] cells = model.GetStateCopy();
                if (!visited.Add(string.Join(",", cells))) continue;
                int[] moves = new int[item.moves.Length + 1];
                Array.Copy(item.moves, moves, item.moves.Length);
                moves[moves.Length - 1] = tile;
                queue.Enqueue((cells, moves));
            }
        }
        throw new InvalidOperationException("퍼즐 해답을 찾지 못했습니다.");
    }
}
