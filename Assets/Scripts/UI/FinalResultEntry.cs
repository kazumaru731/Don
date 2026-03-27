using UnityEngine;
using UnityEngine.UI;

namespace DonGame2D.UI
{
    public class FinalResultEntry : MonoBehaviour
    {
        public Text rankText;
        public Text nameText;
        public Text scoreText;

        public void Setup(int rank, string name, int score, bool isLocal)
        {
            if (rankText != null) rankText.text = rank.ToString();
            if (nameText != null) 
            {
                nameText.text = name;
                if (isLocal) nameText.color = Color.yellow; // 自分は分かりやすく
            }
            if (scoreText != null) scoreText.text = $"{score} Credits";
        }
    }
}
