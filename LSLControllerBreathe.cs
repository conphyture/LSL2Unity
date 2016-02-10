using UnityEngine;
using System.Collections;
using LSL;
using System.Threading;
using System.Collections.Generic;
using System;

// NB: will drop connection in no sample received for 1s
// TODO: catch exception (?)

public class LSLControllerBreathe : MonoBehaviour
{

	// info about the stream we seek
	public string streamName = "breath";
	public string streamType = "tobe";

	// put <= 0 to disable
	public int maxWindowSize = 10;

	// output value
	public float value = 0; // Between 0 and 1

	// FIXME: handle only one channel (+1 for stim from OpenViBE gipsa box)
	private float[] sample = new float[2];

	// used for computing sliding window
	float realTime = 0;
	float lastTime = 0;

	liblsl.StreamInlet inlet = null;
	private Thread dataThread;
	private bool finished = false;

	private bool init = false;


	//Timer

	List<float> listMax = new List<float> ();
	List<float> listMin = new List<float> ();
	
	// Use this for initialization
	void Start ()
	{
		
		dataThread = new Thread (new ThreadStart (fetchData));
		dataThread.Start ();

		// init sliding window with default value
		listMax.Add (value);
		listMin.Add (value);

		lastTime = Time.realtimeSinceStartup;
		realTime = Time.realtimeSinceStartup;
	}

	private bool isConnected() {
		return inlet != null;
	}

	// @return value fetch from LSL stream, should check that still connected after call to be sure that a new value were read
	private float readRawValue() {
		if (isConnected ()) {
			// 1s timeout, if no sample by then, drop
			double timestamp = 0; // return value for no sample
			try {
				timestamp = inlet.pull_sample (sample, 1);
			} catch (TimeoutException) {
				Debug.Log ("Timeout");
			} catch (liblsl.LostException) {
				Debug.Log ("Connection lost");
			}
			if (timestamp == 0) {
				Debug.Log ("No sample, drop connection");
				inlet = null;
			}
			// got sample, let's process it
			else {
				return sample [0];
			}
		}
		// poor default
		return -1;
	}


	// look-up stream and fetch first value
	private void connect() {
		Debug.Log ("Connect to LSL stream type: " + streamType);
		// wait until the correct type shows up
		liblsl.StreamInfo[] results = liblsl.resolve_stream ("type", streamType, 1, 0.5f);
		if (results.Length <= 0) {
			Debug.Log ("No streams found");
			return;
		} else {
			Debug.Log ("Found " + results.Length + " streams, looking for name: " + streamName);
		}
		liblsl.StreamInlet tmpInlet;
		for (int i=0; i < results.Length; i++) {
			// open an inlet and print some interesting info about the stream (meta-data, etc.)
			tmpInlet = new liblsl.StreamInlet (results [i]);
			try {
				liblsl.StreamInfo info = tmpInlet.info ();
				Debug.Log ("Stream number: " + i + ", name: " + info.name ());
				if (info.name ().Equals (streamName)) {
					Debug.Log ("Stream found.");
					inlet = tmpInlet;
					break;
				}
			}
			// could lost stream while looping
			catch (TimeoutException) {
				Debug.Log ("Timeout while fetching info.");
				continue;
			} catch (liblsl.LostException) {
				Debug.Log ("Connection lost while fetching info.");
				continue;
			}
		}

		if (isConnected () && !init) {
			float firstValue = readRawValue();
			// may *again* disconnect while reading value
			if (isConnected ()) {
				listMax [0] = firstValue;
				listMax [0] = firstValue;
				init = true;
			}
		} else {
			Debug.Log ("Stream not found.");
		}
	}

	// if sliding window is used, will scale between 0 and 1 over
	// will update sliding window with rawValue and return scale
	private float getAutoscale(float rawValue) {
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
		if (rawValue > listMax [currentItem - 1]) {
			listMax [currentItem - 1] = rawValue;
		}
		if (rawValue < listMin [currentItem - 1]) {
			listMin [currentItem - 1] = rawValue;
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
		
		return (rawValue - min) / (max - min);
	}



	void fetchData ()
	{
		while (!finished) {
			// no inlet yet (or dropped), try to connect
			if (!isConnected()) {
				connect ();
			} 
			// update value otherwise
			else {
				value = getAutoscale(readRawValue());
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
