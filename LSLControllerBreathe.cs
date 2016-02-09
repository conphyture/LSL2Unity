using UnityEngine;
using System.Collections;
using LSL;
using System.Threading;
using System.Collections.Generic;

// NB: will drop connection in no sample received for 1s
// TODO: catch exception (?)

public class LSLControllerBreathe : MonoBehaviour
{

	// info about the stream we seek
	public string streamName = "breath";
	public string streamType = "tobe";
	private float[] sample = new float[2];
	float realTime = 0;
	float lastTime = 0;
	liblsl.StreamInlet inlet = null;
	private Thread dataThread;
	private bool finished = false;
	public float Breathe = 0; // Between 0 and 1

	public int maxWindowSize = 10;

	//Timer

	List<float> listMax = new List<float> ();
	List<float> listMin = new List<float> ();
	
	// Use this for initialization
	void Start ()
	{
		
		dataThread = new Thread (new ThreadStart (GetData));
		dataThread.Start ();

		listMax.Add (0.0f);
		listMin.Add (0.0f);

		lastTime = Time.realtimeSinceStartup;
		realTime = Time.realtimeSinceStartup;
	}
	
	void GetData ()
	{
		while (!finished) {
			if (inlet != null) {
				// 1s timeout, if no sample by then, drop
				Debug.Log ("pulling");
				double timestamp = inlet.pull_sample (sample, 1);
				Debug.Log ("pulled");
				if (timestamp == 0.0) {
					Debug.Log ("Connection lost");
					inlet = null;
				} 
				// got sample, let's process it
				else {

					if (listMax.Count < maxWindowSize) {
						if (listMax.Count < (realTime - lastTime)) {
							listMax.Add (float.MinValue);
						}
						if (listMin.Count < (realTime - lastTime)) {
							listMin.Add (float.MaxValue);
						}
					} else {
						if (lastTime < (realTime - 1.0f)) {
							lastTime = realTime;
							listMax.RemoveAt (0);
							listMin.RemoveAt (0);
							listMax.Add (float.MinValue);
							listMin.Add (float.MaxValue);
						}
					}


					int currentItem = listMax.Count;
					//Debug.Log(currentItem);
					if (sample [0] > listMax [currentItem - 1]) {
						listMax [currentItem - 1] = sample [0];
					}
					if (sample [0] < listMin [currentItem - 1]) {
						listMin [currentItem - 1] = sample [0];
					}

					float min = float.MaxValue;
					float max = float.MinValue;
					foreach (float element in listMin) {
						if (element < min) {
							min = element;
						}
					}
					foreach (float element in listMax) {
						if (element > max) {
							max = element;
						}
					}

					Breathe = (sample [0] - min) / (max - min);
					//Debug.Log (Breathe);
				}
			} 
			// no inlet yet (or dropped), try to connect
			else {
				Debug.Log ("Connect to LSL stream type: " + streamType);
				// wait until the correct type shows up
				liblsl.StreamInfo[] results = liblsl.resolve_stream ("type", streamType, 1, 0.5f);
				if (results.Length <= 0) {
					Debug.Log ("No streams found");
					continue;
				} else {
					Debug.Log ("Found " + results.Length + " streams, looking for name: " + streamName);
				}
				liblsl.StreamInlet tmpInlet;
				for (int i=0; i < results.Length; i++) {
					// open an inlet and print some interesting info about the stream (meta-data, etc.)
					tmpInlet = new liblsl.StreamInlet (results [i]);
					liblsl.StreamInfo info = tmpInlet.info ();
					Debug.Log ("Stream number: " + i + ", name: " + info.name ());
					if (info.name ().Equals (streamName)) {
						Debug.Log ("Stream found.");
						inlet = tmpInlet;
						break;
					}
				}

				if (inlet != null) {
					inlet.pull_sample (sample, 0.5f);
					listMax [0] = sample [0];
					listMin [0] = sample [0];
				} else {
					Debug.Log ("Stream not found.");
				}
			}
		}
	}
	
	// Update is called once per frame
	void Update ()
	{
		realTime = Time.realtimeSinceStartup;
	}
	
	void OnApplicationQuit ()
	{
		
		finished = true;
	}
}
