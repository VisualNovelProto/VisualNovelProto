using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class MoveScene : MonoBehaviour
{
    public void GoToStage1()
    {
        SceneManager.LoadScene("InGameStoryScene");
    }
}
