using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>명시적으로 실행하는 테스트 씬의 Play Mode 통합 검증입니다.</summary>
public static class PlayerInteractionTestValidation
{
    private const string Running = "InteractionTest.Validation";
    private const string Report = "output/interaction-test/validation.txt";
    private static IEnumerator _routine;
    private static bool _failed;

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Player Interaction/Run Play Mode Checks")]
    public static void Run()
    {
        PlayerInteractionTestSceneBuilder.CreateScene();
        Directory.CreateDirectory("output/interaction-test");
        File.WriteAllText(Report, "Player interaction Play Mode checks\n");
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    public static void ApplyBackgroundAndRun()
    {
        PlayerInteractionTestSceneBuilder.ApplyGroundBackground();
        Run();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Running, false))
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
            SessionState.SetBool(Running, false);
            Application.logMessageReceived -= OnLog;
            if (Application.isBatchMode)
                EditorApplication.Exit(File.ReadAllText(Report).Contains("FAIL") ? 1 : 0);
        }
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return;
        _failed = true;
        File.AppendAllText(Report, "FAIL runtime: " + message + "\n" + stack + "\n");
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
        EditorApplication.update -= Tick;
        File.AppendAllText(Report, _failed ? "FAIL completed\n" : "PASS completed\n");
        EditorApplication.ExitPlaymode();
    }

    private static IEnumerator Validate()
    {
        PlayerInteractionTestBootstrap test = UnityEngine.Object.FindFirstObjectByType<PlayerInteractionTestBootstrap>();
        float deadline = Time.realtimeSinceStartup + 20;
        while (!test.IsReady && Time.realtimeSinceStartup < deadline)
            yield return null;
        Check(test.IsReady, "Automatic host and player spawn");
        Check(test.Player.GetComponent<Unity.Netcode.NetworkObject>().IsOwner, "Local player ownership");
        ParallaxBackground background = UnityEngine.Object.FindFirstObjectByType<ParallaxBackground>();
        Check(background != null && background.name == "ForestBackground_ParallaxTest", "SungjunWork ground background present");
        var backgroundData = new SerializedObject(background);
        Check(backgroundData.FindProperty("_outputCamera").objectReferenceValue == Camera.main,
            "Ground background connected to output camera");
        ParallaxCloudLoop clouds = background.GetComponentInChildren<ParallaxCloudLoop>();
        Transform cloudGroup = clouds.transform.GetChild(0);
        Vector3 initialCloudPosition = cloudGroup.localPosition;
        int count = 0;
        foreach (var item in ServiceManager.Inst.InventoryService.GetInventoryItems())
        {
            if (!DataManager.Inst.ReadData<RuneData>().ContainsKey(item.Id))
                continue;
            Check(item.Count == 1, "Owned rune " + item.Id);
            count++;
        }
        Check(count == DataManager.Inst.ReadData<RuneData>().Count, "All rune IDs granted exactly once");

        float until = Time.time + 1;
        while (Time.time < until) yield return null;
        Capture("output/interaction-test/scene.png");
        Check(cloudGroup.localPosition != initialCloudPosition, "Ground clouds animate after initialization");
        Check(Mathf.Abs(Camera.main.transform.position.x) < 0.05f && Mathf.Abs(Camera.main.transform.position.y) < 0.05f,
            "Camera constrained to one 60 x 33.75 screen");

        test.Player.PlaceAtPosition(new Vector3(-19.6f, -12.98f, 0), true);
        until = Time.time + 0.5f;
        while (Time.time < until) yield return null;
        Check(test.Player.CanStartGroundAttack, "Spawned player can attack from ground");
        var attack = test.Player.GetComponent<PlayerRuneAttackController>();
        attack.RequestAttack(true);
        attack.ReleaseAttack();
        until = Time.time + 1.5f;
        while (Time.time < until) yield return null;

        var equipment = test.Player.GetComponent<PlayerRuneEquipmentController>();
        Check(equipment.RequestSetRune(0, "rune_projectile"), "Request rune equip");
        until = Time.time + 0.3f;
        while (Time.time < until) yield return null;
        Check(equipment.GetEquippedRuneId(0) == "rune_projectile", "Server applies rune equipment");
        test.Player.PlaceAtPosition(new Vector3(-23f, -12.98f, 0), true);
        until = Time.time + 0.4f;
        while (Time.time < until) yield return null;
        attack.RequestAttack(true);
        attack.ReleaseAttack();
        until = Time.time + 2f;
        while (Time.time < until) yield return null;
        equipment.RequestSetRune(0, "");

        // 배치 에디터에는 포커스된 Game View가 없으므로 검사용 복제 설정을 사용합니다.
        InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        test.Player.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
        test.Player.PlaceAtPosition(new Vector3(-8, -12.98f, 0), true);
        until = Time.time + 0.5f;
        while (Time.time < until) yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        InputSystem.Update();
        until = Time.time + 0.1f;
        while (Time.time < until) yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        until = Time.time + 1.5f;
        while (Time.time < until) yield return null;
        Check(test.Player.GetComponent<Rigidbody2D>().position.y > -10f,
            "Jump through and land on thin platform (position " + test.Player.transform.position +
            ", map " + test.Player.GetComponent<PlayerInput>().currentActionMap?.name + ")");

        Vector2[] tops = { new Vector2(-8, -10), new Vector2(-1, -7), new Vector2(6, -4),
            new Vector2(13, -1), new Vector2(20, 2) };
        float[] widths = { 4, 4, 4, 4, 8 };
        Transform feet = test.Player.transform.Find("GroundCheck");
        for (int i = 0; i < tops.Length - 1; i++)
        {
            float direction = Mathf.Sign(tops[i + 1].x - tops[i].x);
            float startX = tops[i].x + direction * (widths[i] * 0.5f - 1f);
            float landingX = tops[i + 1].x - direction * (widths[i + 1] * 0.5f - 1.3f);
            test.Player.PlaceAtPosition(new Vector3(startX, tops[i].y + 0.02f, 0), direction > 0);
            until = Time.time + 0.4f;
            while (Time.time < until) yield return null;
            Key move = direction > 0 ? Key.D : Key.A;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(move, Key.Space));
            until = Time.time + 0.08f;
            while (Time.time < until) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(move));
            until = Time.time + 1.2f;
            while ((landingX - feet.position.x) * direction > 0 && Time.time < until)
                yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            until = Time.time + 1f;
            while (Time.time < until) yield return null;
            Check(Mathf.Abs(feet.position.y - tops[i + 1].y) < 0.2f,
                "Playable platform jump " + (i + 1) + " -> " + (i + 2) + " (feet " + feet.position + ")");
        }

        test.Player.PlaceAtPosition(new Vector3(-8, -9.98f, 0), true);
        until = Time.time + 0.3f;
        while (Time.time < until) yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.Space));
        until = Time.time + 0.35f;
        while (Time.time < until) yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        until = Time.time + 1f;
        while (Time.time < until) yield return null;
        Check(feet.position.y < -12.5f, "S + Space drops through thin platform");
        InputSystem.RemoveDevice(keyboard);
        InputSystem.RemoveDevice(mouse);

        var runeUiTask = UIManager.Inst.OpenRuneInventoryUIAsync();
        var awaiter = runeUiTask.GetAwaiter();
        until = Time.time + 15f;
        while (!awaiter.IsCompleted && Time.time < until) yield return null;
        Check(awaiter.IsCompleted, "Rune UI loading completes");
        RuneInventoryUI view = awaiter.GetResult();
        Check(view != null && ServiceManager.Inst.NetworkRuneService.BindRuneInventoryView(view), "Rune UI binds to owned inventory");
        UIManager.Inst.CloseRuneInventoryUI();
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
        File.AppendAllText(Report, "PASS " + message + "\n");
    }

    private static void Capture(string path)
    {
        Camera camera = Camera.main;
        RenderTexture texture = RenderTexture.GetTemporary(1920, 1080, 24);
        RenderTexture previous = RenderTexture.active;
        Rect previousRect = camera.rect;
        camera.rect = new Rect(0, 0, 1, 1);
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        Texture2D image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = null;
        camera.rect = previousRect;
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(texture);
        UnityEngine.Object.DestroyImmediate(image);
    }
}
