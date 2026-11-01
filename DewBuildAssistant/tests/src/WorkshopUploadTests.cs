using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DewBuildAssistant.controller;
using DewBuildAssistant.model;
using DewBuildAssistant.util;
using Steamworks;

internal static class WorkshopUploadTests
{
    // 顺序执行真实上传协程及嵌套 Wait，所有文件只写入独立临时目录。
    public static void Run(string fixtures, Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "DewBuildAssistant-upload-" + Guid.NewGuid().ToString("N"));
        string previous = UnityEngine.Application.persistentDataPath;
        try
        {
            UnityEngine.Application.persistentDataPath = root;
            var guide = WorkshopBuildCodec.Decode(File.ReadAllBytes(Path.Combine(fixtures, "build-v5.json")));
            string folder = Path.Combine(root, "BD-Assistant", "10", "Hero_Test-0");
            string idFile = Path.Combine(folder, "publishedfileid.txt");
            string backup = idFile + ".missing-100";
            Directory.CreateDirectory(folder);

            void Setup(ulong id = 0)
            {
                SteamUGC.Reset();
                SteamUGC.IdFile = idFile;
                if (File.Exists(idFile)) File.Delete(idFile);
                if (File.Exists(backup)) File.Delete(backup);
                if (id != 0) File.WriteAllText(idFile, id.ToString(), new UTF8Encoding(false));
            }
            (ulong id, string error) Upload()
            {
                ulong id = 0;
                string error = null;
                int completions = 0;
                using (var client = new WorkshopClient())
                    Drain(client.Upload(guide, 0, "Hero_Test", (value, failure) => { id = value; error = failure; completions++; }));
                check(completions == 1, "upload completes exactly once");
                return (id, error);
            }
            void Create(ulong id = 200, EResult result = EResult.k_EResultOK) => SteamUGC.Creates.Enqueue(new SteamAPICall_t
                { Result = new CreateItemResult_t { m_eResult = result, m_nPublishedFileId = new PublishedFileId_t(id) } });
            void Submit(EResult result = EResult.k_EResultOK, bool failed = false) => SteamUGC.Submits.Enqueue(new SteamAPICall_t
                { Result = new SubmitItemUpdateResult_t { m_eResult = result }, Failed = failed });

            Setup(); Create(); Submit();
            var outcome = Upload();
            check(outcome.id == 200 && outcome.error == null && SteamUGC.CreateCount == 1, "fresh upload creates one item");
            check(SteamUGC.PrivateIds.SequenceEqual(new ulong[] { 200 }), "fresh upload is private");
            check(SteamUGC.SavedIdsAtSubmit.SequenceEqual(new ulong[] { 200 }), "created ID persists before submission");
            check(WorkshopBuildCodec.Decode(File.ReadAllBytes(Path.Combine(SteamUGC.ContentPath, WorkshopBuildCodec.FileName))).Hero == guide.Hero,
                "upload content survives JSON round trip");

            Setup(100); Submit(); outcome = Upload();
            check(outcome.id == 100 && outcome.error == null && SteamUGC.CreateCount == 0, "existing item updates without creation");
            check(SteamUGC.PrivateIds.Count == 0 && !File.Exists(backup), "existing visibility and association are retained");

            Setup(100); Submit(EResult.k_EResultFileNotFound); Create(); Submit(); outcome = Upload();
            check(outcome.id == 200 && outcome.error == null && SteamUGC.CreateCount == 1, "missing old item recreates once");
            check(SteamUGC.UpdatedIds.SequenceEqual(new ulong[] { 100, 200 }), "recovery submits to replacement ID");
            check(File.ReadAllText(backup, Encoding.UTF8) == "100" && File.ReadAllText(idFile, Encoding.UTF8) == "200", "recovery archives old ID and saves replacement");
            check(SteamUGC.PrivateIds.SequenceEqual(new ulong[] { 200 }) && SteamUGC.SavedIdsAtSubmit.SequenceEqual(new ulong[] { 100, 200 }),
                "replacement stays private and is saved before submission");

            Setup(100); Submit(EResult.k_EResultFileNotFound); Create(); Submit(EResult.k_EResultFileNotFound); outcome = Upload();
            check(SteamUGC.CreateCount == 1 && SteamUGC.UpdatedIds.Count == 2 && outcome.error.Contains("UploadSubmitError:200"), "replacement failure stops after one recreation with item context");
            check(File.ReadAllText(idFile, Encoding.UTF8) == "200", "failed replacement submission retains its draft ID");
            SteamUGC.Reset(); SteamUGC.IdFile = idFile; Submit(); outcome = Upload();
            check(outcome.id == 200 && outcome.error == null && SteamUGC.CreateCount == 0, "retry updates retained replacement draft");

            foreach (var result in new[] { EResult.k_EResultFail, EResult.k_EResultAccessDenied })
            {
                Setup(100); Submit(result); outcome = Upload();
                check(SteamUGC.CreateCount == 0 && !File.Exists(backup) && File.ReadAllText(idFile, Encoding.UTF8) == "100",
                    "non-missing failure preserves original ID without creating: " + result);
                check(outcome.error.Contains("UploadSubmitError:100") && outcome.error.Contains(result.ToString()), "update failure reports stage and code");
            }
            Setup(100); Submit(EResult.k_EResultFileNotFound, true); outcome = Upload();
            check(SteamUGC.CreateCount == 0 && !File.Exists(backup) && outcome.error != null, "transport failure never triggers missing-item recovery");

            Setup(); Create(0, EResult.k_EResultFail); outcome = Upload();
            check(!File.Exists(idFile) && SteamUGC.UpdatedIds.Count == 0 && outcome.error.Contains("UploadCreateError"), "creation failure reports stage without saving an ID");

            Setup(100); Submit(EResult.k_EResultFileNotFound); Create(0, EResult.k_EResultFail); outcome = Upload();
            check(File.ReadAllText(backup, Encoding.UTF8) == "100" && File.ReadAllText(idFile, Encoding.UTF8) == "100" && outcome.error.Contains("UploadCreateError"),
                "failed recovery creation preserves original association and backup");

            Setup(); Create(); Submit(EResult.k_EResultFileNotFound); outcome = Upload();
            check(SteamUGC.CreateCount == 1 && !File.Exists(backup) && outcome.error != null, "fresh item FileNotFound never enters recreation loop");

            Setup(100); SteamUGC.AcceptMetadata = false; outcome = Upload();
            check(SteamUGC.CreateCount == 0 && SteamUGC.SavedIdsAtSubmit.Count == 0 && outcome.error != null && !File.Exists(backup), "metadata rejection keeps association without submitting or recreating");
        }
        finally
        {
            UnityEngine.Application.persistentDataPath = previous;
            // root 由固定临时目录与本测试的随机名称组成，清理仅限本次目录。
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // 模拟控制器的协程展开，意外等待或循环会在固定步数内使测试失败。
    private static void Drain(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        try
        {
            int steps = 0;
            while (stack.Count > 0)
            {
                if (++steps > 200) throw new Exception("Upload coroutine did not complete");
                if (!stack.Peek().MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (stack.Peek().Current is IEnumerator child) stack.Push(child);
            }
        }
        finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
    }
}
