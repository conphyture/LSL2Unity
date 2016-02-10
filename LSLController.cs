using UnityEngine;
using System.Collections;
using LSL;
using System.Threading;
using System.Collections.Generic;
using System;



// fetch LSL data in the background, possible to get raw value or to use a sliding window for autoscaling between 0 and 1 (default range).
// a "trigger" is set as soon as a value is positive, it is up to the client to lower the flag.

// NB: will drop connection in no sample received for 1s

// NB: does not handle chunks(?), fetch only first channel

public class LSLController : MonoBehaviour
{
	// will return that if nothing comes
	private const float defaultValue = 0;

	// info about the stream we seek
	public string streamName = "breath";
	public string streamType = "tobe";

	// put <= 0 to disable
	public int maxWindowSize = 10;

	// output value
	public float value = defaultValue; // Between 0 and 1

	// trigger mechanism, set to last value read
	public bool lastTrigger = false;
	// flag raised for each trigger, to be lowered by clients
	public bool trigger = false;

	// will it spam debug output?
	public bool verbose = false;

	private int nbChannels = 1;
	private float[] sample;

	// used for computing sliding window
	float realTime = 0;
	float lastTime = 0;

	// misc internal state
	liblsl.StreamInlet inlet = null;
	private Thread dataThread;
	private bool finished = false;

	// if we got our first value to init sliding window
	private bool init = false;
	//Timer for sliding window
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

	private void log(String mes) {
		if (verbose) {
			Debug.Log (mes);
		}
	}

	// @return value fetch from LSL stream, should check that still connected after call to be sure that a new value were read
	private float readRawValue() {
		if (isConnected ()) {
			// 1s timeout, if no sample by then, drop
			double timestamp = 0; // return value for no sample
			try {
				timestamp = inlet.pull_sample (sample, 1);
			} catch (TimeoutException) {
				log ("Timeout");
			} catch (liblsl.LostException) {
				log ("Connection lost");
			}
			if (timestamp == 0) {
				log ("No sample, drop connection");
				inlet = null;
			}
			// got sample, let's process it
			else {
				return sample [0];
			}
		}
		// poor default
		return defaultValue;
	}


	// look-up stream and fetch first value
	private void connect() {
		log ("Connect to LSL stream type: " + streamType);
		// wait until the correct type shows up
		liblsl.StreamInfo[] results = liblsl.resolve_stream ("type", streamType, 1, 0.5f);
		if (results.Length <= 0) {
			log ("No streams found");
			return;
		} else {
			log ("Found " + results.Length + " streams, looking for name: " + streamName);
		}
		liblsl.StreamInlet tmpInlet;
		for (int i=0; i < results.Length; i++) {
			// open an inlet and print some interesting info about the stream (meta-data, etc.)
			tmpInlet = new liblsl.StreamInlet (results [i]);
			try {
				liblsl.StreamInfo info = tmpInlet.info ();
				log ("Stream number: " + i + ", name: " + info.name ());
				if (info.name ().Equals (streamName)) {
					nbChannels = info.channel_count();
					log ("Stream found with " + nbChannels + " channels");
					if (nbChannels < 1) {
						log ("Error, no channels found, skip.");
					}
					else {
						inlet = tmpInlet;
						sample = new float[nbChannels];
						break;
					}
				}
			}
			// could lost stream while looping
			catch (TimeoutException) {
				log ("Timeout while fetching info.");
				continue;
			} catch (liblsl.LostException) {
				log ("Connection lost while fetching info.");
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
			log ("Stream not found.");
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
				if (maxWindowSize > 0) {
					value = getAutoscale(readRawValue());
				}
				else {
					value = readRawValue();
				}

				if (value > 0)
				{
					if (lastTrigger == false)
					{
						log("Beat");
						trigger = true;
					}
					lastTrigger = true;
				}
				else
				{
					lastTrigger = false;
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
