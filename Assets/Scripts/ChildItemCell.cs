using UnityEngine;
using UnityEngine.UI;

public class ChildItemCell : TreeChildItemCell
{
        public Text nameTxt;
        public override void SetData(object data)
        {
                base.SetData(data);
                nameTxt.text = data.ToString();
        }
}