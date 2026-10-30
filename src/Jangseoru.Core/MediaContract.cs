using System.Collections.Generic;

namespace Jangseoru.Core
{
    /// <summary>The two game methods through which a big picture or a video is shown; included in GameContract.Required.</summary>
    public static class MediaContract
    {
        /// <summary>Iterator that shows a big picture; the first parameter is the script key (the game loads "pic_" + key).</summary>
        public static readonly MemberSpec ShowBigPicture =
            MemberSpec.Method(GameContract.StoryMainUI, "ShowBigPicture", "System.Collections.IEnumerator", "System.String", "System.Int32");

        /// <summary>Iterator that plays a video; the first parameter is the script key (the game loads "video_" + key).</summary>
        public static readonly MemberSpec VideoOpen =
            MemberSpec.Method("Mortal.Core.CgVideoPanel", "Open", "System.Collections.IEnumerator", "System.String", "System.Single");

        public static readonly IReadOnlyList<MemberSpec> Required = new[] { ShowBigPicture, VideoOpen };
    }
}
