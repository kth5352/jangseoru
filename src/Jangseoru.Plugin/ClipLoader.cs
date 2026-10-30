using System;
using System.Reflection;
using Jangseoru.Core;
using UnityEngine;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// Addressables.LoadAssetAsync&lt;VideoClip&gt; / Release through reflection (resolved from <see cref="GameContract"/>), so a
    /// changed Addressables version disables the videos instead of breaking JIT. Handles are boxed
    /// AsyncOperationHandle&lt;Sprite&gt; values; every handle returned by <see cref="Load"/> must be passed to
    /// <see cref="Release"/> exactly once. <see cref="Live"/> counts handles not yet released. Never throws.
    /// </summary>
    internal sealed class ClipLoader
    {
        private readonly MethodInfo load, release, isValid;
        private readonly PropertyInfo isDone, status, result;
        private readonly object succeeded;
        private readonly ILog log;
        private bool loadWarned, releaseWarned;

        public int Live { get; private set; }

        private ClipLoader(GameAccess access, ILog log)
        {
            this.log = log;
            load = access.LoadAssetAsyncDef.MakeGenericMethod(typeof(UnityEngine.Video.VideoClip));
            release = access.ReleaseDef.MakeGenericMethod(typeof(UnityEngine.Video.VideoClip));
            var handle = access.HandleDef.MakeGenericType(typeof(UnityEngine.Video.VideoClip));
            const BindingFlags pub = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            isDone = handle.GetProperty(GameContract.HandleIsDone.Name, pub);
            status = handle.GetProperty(GameContract.HandleStatus.Name, pub);
            result = handle.GetProperty(GameContract.HandleResult.Name, pub);
            isValid = handle.GetMethod(GameContract.HandleIsValid.Name, pub, null, Type.EmptyTypes, null);
            succeeded = Enum.Parse(access.HandleStatusType, GameContract.StatusSucceeded.Name);
            if (isDone == null || status == null || result == null || isValid == null)
                throw new MissingMemberException("AsyncOperationHandle<VideoClip> members");
        }

        /// <summary>Null when Addressables could not be bound; the callers then play no videos.</summary>
        public static ClipLoader Create(GameAccess access, ILog log)
        {
            try
            {
                if (access == null || !access.LibraryUiResolved) return null;
                return new ClipLoader(access, log);
            }
            catch (Exception e)
            {
                try { log?.Warn("videos disabled: Addressables binding failed (" + e.GetType().Name + ")"); } catch { }
                return null;
            }
        }

        /// <summary>Starts loading; null if the call failed (nothing to release then). <paramref name="purpose"/> labels the log.</summary>
        public object Load(string key, string purpose = "video")
        {
            try
            {
                var handle = load.Invoke(null, new object[] { key });
                if (handle == null) return null;
                Live++;
                return handle;
            }
            catch (Exception e)
            {
                if (!loadWarned) { loadWarned = true; try { log?.Warn((purpose ?? "video") + " load failed for " + key + " (" + Inner(e) + ")"); } catch { } }
                return null;
            }
        }

        /// <summary>True once the operation has finished; <paramref name="sprite"/> is set only if it succeeded.</summary>
        public bool Poll(object handle, out UnityEngine.Video.VideoClip clip)
        {
            clip = null;
            try
            {
                if (handle == null) return true;
                if (!(bool)isValid.Invoke(handle, null)) return true;
                if (!(bool)isDone.GetValue(handle, null)) return false;
                if (Equals(status.GetValue(handle, null), succeeded)) clip = result.GetValue(handle, null) as UnityEngine.Video.VideoClip;
                return true;
            }
            catch (Exception) { return true; }
        }

        public void Release(object handle)
        {
            if (handle == null) return;
            Live--;
            try
            {
                if ((bool)isValid.Invoke(handle, null)) release.Invoke(null, new[] { handle });
            }
            catch (Exception e)
            {
                if (!releaseWarned) { releaseWarned = true; try { log?.Warn("video handle release failed (" + Inner(e) + ")"); } catch { } }
            }
        }

        private static string Inner(Exception e) => (e is TargetInvocationException && e.InnerException != null ? e.InnerException : e).GetType().Name;
    }
}
