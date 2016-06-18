using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;

namespace SmartCmdArgs.Helper
{
    public static class MSBuildUtils
    {
        public static ICollection<string> GetMSBuildPropNamesForProject(Project project)
        {
            Microsoft.Build.Construction.ProjectRootElement root = Microsoft.Build.Construction.ProjectRootElement.Open(project.FullName);
            Microsoft.Build.Evaluation.Project proj = new Microsoft.Build.Evaluation.Project(root);
            return proj.Properties.Select(prop => prop.Name).ToList();
        }
    }
}
