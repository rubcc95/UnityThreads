public class UnityThreadsTest : MonoBehaviour
{
    void Start()
    {
        //Creating the 1st thread, it starts inmediatly and lasts 10 seconds.
        var thread1 = UnityThreads.UpdateThread("Thread1", Thread1, 0, 10);
        
        //This messages will spawn when thread is paused/resumed.
        thread1.OnPause.AddListener(() => { Debug.Log("Thread1 paused");  });
        thread1.OnResume.AddListener(() => { Debug.Log("Thread1 resumed"); });
        
        //Lock the thread, so when it ends it won't be destroyed... and we can reuse it.
        thread1.DestroyWhenEnded = false;

        //Creating the 2nd thread, it starts inmediatly and lasts 15 seconds
        var thread2 = UnityThreads.UpdateThread("Thread2", Thread2, 0, 15);
        
        //When thread2 ends, it restart both threads
        thread2.OnEnd.AddListener(() => 
        {
            Debug.Log("Restarting both threads");
            UnityThread.Current.Restart();
            UnityThreads.Get("Thread1").RestartCompletely();             
        });
    }

    //Thread1 main action.
    void Thread1()
    {        
        //When 5 seconds left to end thread1, it's paused.
        if (UnityThread.Current.EndTime < 5 && UnityThread.Current.Properties["PausedOnce"] != true) 
        {
            UnityThread.Current.Properties["PausedOnce"] = true;
            UnityThread.Current.Pause();            
        }        
    }

    //Thread2 main action
    void Thread2()
    {
        ////When 5 seconds left to end thread2, thread1 is resumed (5 seconds after it had been paused).
        if(UnityThread.Current.EndTime < 5)
        {
            UnityThreads.Get("Thread1").Resume();
        }         
    }
}
