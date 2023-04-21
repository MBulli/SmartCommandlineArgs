using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SmartCmdArgs.Helper
{
    static class TreeHelper
    {
        public static T FindVisualChild<T>(DependencyObject obj)
            where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);

                var childAsT = child as T;
                if (childAsT != null)
                    return childAsT;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        public static T FindAncestorOrSelf<T>(DependencyObject obj)
            where T : DependencyObject
        {
            while (obj != null)
            {
                T objTest = obj as T;
                if (objTest != null)
                    return objTest;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }

        public static T FindAncestorOrSelf<T>(DependencyObject obj, string name)
            where T : FrameworkElement
        {
            while (obj != null)
            {
                if (obj is T objTest && objTest.Name == name)
                    return objTest;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }

        public static T FindAncestor<T>(DependencyObject obj, int skipLevels = 0)
            where T : DependencyObject
        {
            if (skipLevels < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(skipLevels));
            }

            do {
                obj = VisualTreeHelper.GetParent(obj);

                if (obj is T objTest)
                    return objTest;
            } while (obj != null);

            return null;
        }
    }
}
