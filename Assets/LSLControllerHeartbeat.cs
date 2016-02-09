using UnityEngine;
using System.Collections;
using LSL;
using System.Threading;

public class LSLControllerHeartbeat : MonoBehaviour {

	private float[] sample = new float[2];

	liblsl.StreamInlet inlet = null;

	private Thread dataThread;
	private bool finished = false;

    public bool LastHeartbeat = false;
    public bool Heartbeat = false;

	// Use this for initialization
	void Start () {
		
		dataThread = new Thread(new ThreadStart(GetData));
		dataThread.Start();
	}

	void GetData()
	{
		while(!finished)
		{
			if (inlet != null)
			{
				inlet.pull_sample(sample, 0.5f);
                //Breathe = sample[];
                if (sample[0] > 0)
                {
                    Debug.Log("Beat");
                    if (LastHeartbeat == false)
                    {
                        Heartbeat = true;
                    }
                    LastHeartbeat = true;
                }
                else
                {
                    LastHeartbeat = false;
                }
            }
			else
			{
				// wait until an EEG stream shows up
				liblsl.StreamInfo[] results = liblsl.resolve_stream("type", "heart", 1, 0.5f);
				
				// open an inlet and print some interesting info about the stream (meta-data, etc.)
				inlet = new liblsl.StreamInlet(results[0]);
				Debug.Log(inlet.info().as_xml());
			}
		}
	}
	
	// Update is called once per frame
	void Update () {

	}
	
	void OnApplicationQuit() {

		finished = true;
	}
}
