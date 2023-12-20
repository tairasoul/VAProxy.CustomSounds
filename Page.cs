using System.Collections;
using UIWindowPageFramework;
using UnityEngine;
using UnityEngine.UI;

namespace VA_CustomSounds
{
    internal class Page : MonoBehaviour
    {
        internal string PageName = "CUSTOM_SOUNDS";

        internal void Awake()
        {
            Register();
        }

        internal void Register()
        {
            StartCoroutine(RegisterWhenFrameworkReady());
        }

        internal IEnumerator RegisterWhenFrameworkReady()
        {
            while (!Framework.Ready)
            {
                yield return null;
            }
            GameObject window = Framework.CreateWindow(PageName);
            Framework.RegisterWindow(window, CreationCallback);
        }

        internal void CreationCallback(GameObject window)
        {
            GameObject Reload = ComponentUtils.CreateButton("Reload sounds.", "reloadsounds");
            GameObject Revert = ComponentUtils.CreateButton("Revert sounds.", "revertsounds");
            Reload.SetParent(window, false);
            Revert.SetParent(window, false);
            RectTransform ReloadTransform = Reload.GetComponent<RectTransform>();
            ReloadTransform.sizeDelta = new Vector2(200, 70);
            ReloadTransform.anchoredPosition = new Vector2(-159.3547f, 0);
            GameObject ReloadItemName = Reload.Find("ItemName");
            Text ReloadText = ReloadItemName.GetComponent<Text>();
            ReloadText.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform ReloadItem = ReloadItemName.GetComponent<RectTransform>();
            ReloadItem.anchoredPosition = new Vector2(-27.1489f, 0);
            RectTransform RevertTransform = Revert.GetComponent<RectTransform>();
            RevertTransform.sizeDelta = new Vector2(200, 70);
            RevertTransform.anchoredPosition = new Vector2(52.3744f, 0);
            GameObject RevertItemName = Revert.Find("ItemName");
            Text RevertText = RevertItemName.GetComponent<Text>();
            RevertText.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform RevertItem = RevertItemName.GetComponent<RectTransform>();
            RevertItem.anchoredPosition = new Vector2(-27.1489f, 0);
            Button BReload = Reload.GetComponent<Button>();
            Button BRevert = Revert.GetComponent<Button>();
            BReload.onClick.AddListener(() => Plugin.Instance.ReloadSounds());
            BRevert.onClick.AddListener(() => Plugin.Instance.RevertSounds());
        }
    }
}
