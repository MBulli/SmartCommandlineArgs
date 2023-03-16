using Microsoft.VisualStudio.Imaging.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartCmdArgs
{
    internal class CustomMonikers
    {
        private static readonly Guid ManifestGuid = new Guid("cafdbdaf-1847-4824-8957-a169d25d0cfb");

        public static ImageMoniker FoProjectNode => new ImageMoniker { Guid = ManifestGuid, Id = 0 };
        public static ImageMoniker CopyCmdLine => new ImageMoniker { Guid = ManifestGuid, Id = 1 };
    }
}
