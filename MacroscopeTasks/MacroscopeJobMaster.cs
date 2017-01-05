﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using RobotsTxt;
using System.Threading;
using System.Diagnostics;

namespace SEOMacroscope
{

	public class MacroscopeJobMaster : Macroscope
	{

		MacroscopeMainForm msMainForm;

		public MacroscopeJobLocker DisplayLock;
		
		/** BEGIN: Configuration **/
		
		int ThreadsMax;
		int ThreadsRunning;
		Boolean ThreadsPaused;
		Boolean ThreadsStop;
		Dictionary<int,Boolean> ThreadsDict;
		
		public string StartUrl { get; set; }
		public int Depth { get; set; }
		public int PageLimit { get; set; }
		public int PageLimitCount { get; set; }
		public Boolean SameSite { get; set; }
		public Boolean ProbeHrefLangs { get; set; }

		/** END: Configuration **/

		int PagesFound;

		Queue<string> UrlQueue;
		public MacroscopeJobLocker UrlQueueLock;
		
		Hashtable History;

		MacroscopeDocumentCollection msDocCollection;
		
		Hashtable Locales;

		MacroscopeRobots msRobots;
				
		/**************************************************************************/

		public MacroscopeJobMaster ( MacroscopeMainForm msMainFormNew )
		{

			msMainForm = msMainFormNew;
			DisplayLock = new MacroscopeJobLocker ();

			ThreadsMax = 4;
			ThreadsRunning = 0;
			ThreadsPaused = false;
			ThreadsStop = false;
			ThreadsDict = new Dictionary<int,Boolean> ();

			ThreadPool.SetMaxThreads( ThreadsMax, ThreadsMax );
						
			Depth = MacroscopePreferences.GetDepth();
			PageLimit = MacroscopePreferences.GetPageLimit();
			PageLimitCount = 0;
			SameSite = MacroscopePreferences.GetSameSite();
			ProbeHrefLangs = MacroscopePreferences.GetProbeHreflangs();
			PagesFound = 0;

			UrlQueue = new Queue<string> ( 4096 );
			UrlQueueLock = new MacroscopeJobLocker ();

			History = Hashtable.Synchronized( new Hashtable ( 4096 ) );

			msDocCollection = new MacroscopeDocumentCollection ();
			
			Locales = Hashtable.Synchronized( new Hashtable ( 32 ) );
			msRobots = new MacroscopeRobots ();

		}

		/**************************************************************************/

		public Boolean Execute ()
		{

			debug_msg( "Run", 1 );

			debug_msg( string.Format( "Start URL: {0}", this.StartUrl ), 1 );

			this.ThreadsPaused = false;
			this.ThreadsStop = false;
			 							
			if( !this.UrlQueuePeek() ) {
				this.UrlQueueAdd( this.StartUrl );
			}

			this.WorkersSpawn();
			
			debug_msg( string.Format( "Pages Found: {0}", this.PagesFound ), 1 );

			debug_msg( "Done", 1 );
								
			this.msMainForm.CallbackScanComplete();

			return( true );
		}

		/**************************************************************************/

		void WorkersSpawn ()
		{

			Boolean bDoRun = true;

			while( bDoRun == true ) {

				//debug_msg( string.Format( "this.ThreadsStop: {0}", this.ThreadsStop.ToString() ) );

				this.UpdateStatusBar();

				if( this.ThreadsStop == true ) {

					debug_msg( string.Format( "WorkersSpawn: {0}", "STOPPING" ) );
					bDoRun = false;
					break;

				} else {

					if( this.ThreadsPaused == true ) {

						debug_msg( string.Format( "WorkersSpawn: {0}", "PAUSED" ) );
						Thread.Sleep( 1000 );

					} else {

						for( int i = 0; i < this.ThreadsMax; i++ ) {
							Boolean bNewThread = ThreadPool.QueueUserWorkItem( this.WorkerStart, null );
							Thread.Sleep( 100 );
						}
						
						Thread.Sleep( 1000 );

						if( this.RunningThreadsCount() == 0 ) {
							if( !this.UrlQueuePeek() ) {
								bDoRun = false;
							}
						}

					}
				
				}
			}
			
			this.UpdateStatusBar();
							
			debug_msg( string.Format( "WorkersSpawn: STOPPED" ) );

		}
		
		/**************************************************************************/
		
		void WorkerStart ( object thContext )
		{
			MacroscopeJobWorker msJobWorker = new MacroscopeJobWorker ( this );
			string sURL = this.UrlQueueGet();
			if( sURL != null ) {
				this.RunningThreadsInc();
				msJobWorker.Execute( sURL );
			}
		}

		/**************************************************************************/

		public void WorkersNotifyDone ( string sURL )
		{
			//debug_msg( string.Format( "WorkersNotifyDone: {0}", sURL ) );
			this.RunningThreadsDec();
			this.UpdateDisplay();
			this.UpdateStatusBar();
		}
		
		/**************************************************************************/

		public void WorkersStop ()
		{
			debug_msg( string.Format( "WorkersStop" ) );
			this.ThreadsStop = true;
		}

		/**************************************************************************/
		
		public Boolean WorkersStopped ()
		{
			Boolean bIsStopped = false;

			
			//debug_msg( string.Format( "WorkersStopped" ) );

			int iThreadCount = this.RunningThreadsCount();
			debug_msg( string.Format( "iThreadCount: {0}", iThreadCount.ToString() ) );

			if( iThreadCount == 0 ) {
				bIsStopped = true;
			}

			

			this.UpdateStatusBar();

			return( bIsStopped );
		}

		/**************************************************************************/
		
		public Boolean WorkersPause ()
		{

			debug_msg( string.Format( "WorkersPause" ) );
			this.ThreadsPaused = true;

			return( this.ThreadsPaused );
		}
		
		/**************************************************************************/

		public void WorkersUnpause ()
		{

			debug_msg( string.Format( "WorkersUnpause" ) );
			this.ThreadsPaused = false;

		}
		
		/**************************************************************************/

		public Boolean IsWorkersPaused ()
		{
			return( this.ThreadsPaused );
		}
		
		/**************************************************************************/

		void RunningThreadsInc ()
		{

			int iThreadId = Thread.CurrentThread.ManagedThreadId;
			this.ThreadsDict[ iThreadId ] = true;
			//debug_msg( string.Format( "iThreadId: {0}", iThreadId.ToString() ) );
			this.ThreadsRunning++;

		}
		
		/**************************************************************************/

		void RunningThreadsDec ()
		{

			if( this.ThreadsRunning > 0 ) {
				int iThreadId = Thread.CurrentThread.ManagedThreadId;
				if( this.ThreadsDict.ContainsKey( iThreadId ) ) {
					this.ThreadsDict.Remove( iThreadId );
				}
				this.ThreadsRunning--;
			}

		}
		
		/**************************************************************************/
				
		public int RunningThreadsCount ()
		{
			int iRunningThreads = 0;

			iRunningThreads = this.ThreadsRunning;

			return( iRunningThreads );
		}

		/**************************************************************************/
		
		public void UrlQueueAdd ( string sURL )
		{
			lock( UrlQueueLock ) {
				//debug_msg( string.Format( "AddUrlQueue: {0}", sURL ) );
				if( !this.HistorySeen( sURL ) ) {
					this.UrlQueue.Enqueue( sURL );
				}
			}
		}
		
		/**************************************************************************/
		
		public string UrlQueueGet ()
		{
			string sURL = null;
			lock( UrlQueueLock ) {
				//debug_msg( string.Format( "GetUrlQueue: {0}", this.UrlQueue.Count.ToString() ) );
				try {
					if( this.UrlQueue.Count > 0 ) {
						sURL = this.UrlQueue.Dequeue();
					}
				} catch( InvalidOperationException ex ) {
					debug_msg( string.Format( "InvalidOperationException: {0}", ex.Message ) );
				}
			}
			return( sURL );
		}
	
		/**************************************************************************/
				
		public Boolean UrlQueuePeek ()
		{
			Boolean bPeek = false;
			lock( UrlQueueLock ) {
				//debug_msg( string.Format( "PeekUrlQueue: {0}", this.UrlQueue.Count.ToString() ) );
				try {
					if( this.UrlQueue.Count > 0 ) {
						bPeek = true;
					}
				} catch( InvalidOperationException ex ) {
					debug_msg( string.Format( "InvalidOperationException: {0}", ex.Message ) );
				}
			}
			return( bPeek );
		}

		/**************************************************************************/
		
		public int UrlQueueCount ()
		{
			int iCount = 0;
			lock( UrlQueueLock ) {
				try {
					if( this.UrlQueue.Count > 0 ) {
						iCount = this.UrlQueue.Count;
					}
				} catch( InvalidOperationException ex ) {
					debug_msg( string.Format( "InvalidOperationException: {0}", ex.Message ) );
				}
			}
			return( iCount );
		}

		/**************************************************************************/
				
		public void HistoryAdd ( string sURL )
		{
			if( !this.History.ContainsKey( sURL ) ) {
				this.History.Add( sURL, true );
			}
		}
		
		/**************************************************************************/
				
		public Boolean HistorySeen ( string sURL )
		{
			Boolean bSeen = false;
			if( this.History.ContainsKey( sURL ) ) {
				bSeen = ( Boolean )this.History[ sURL ];
			}
			return( bSeen );
		}

		/**************************************************************************/

		public MacroscopeDocumentCollection GetDocCollection ()
		{
			return( this.msDocCollection );
		}
		
		/**************************************************************************/
		
		public Hashtable GetLocales ()
		{
			return( this.Locales );
		}

		/**************************************************************************/
		
		public void AddLocales ( string sLocale )
		{			
			if( !this.Locales.ContainsKey( sLocale ) ) {
				this.Locales[ sLocale ] = sLocale;
			}
		}

		/**************************************************************************/
		
		public MacroscopeRobots GetRobots ()
		{
			return( this.msRobots );
		}
		
		/**************************************************************************/

		public void UpdateDisplay ()
		{
			if( this.ThreadsStop == true ) {
				return;
			}
			lock( this.DisplayLock ) {
				try {
					this.msMainForm.UpdateDisplayStructure( this );
				} catch( ArgumentException ex ) {
					debug_msg( string.Format( "UpdateDisplay: {0}", ex.Message ), 1 );
				}
			}
		}

		/**************************************************************************/

		public void UpdateStatusBar ()
		{
			this.msMainForm.UpdateStatusBar();
		}

		/**************************************************************************/
		
		/*
		public Boolean Recurse ( string sParentURL, string sURL, int iDepth )
		{
			MacroscopeDocument msDoc = new MacroscopeDocument ( sURL );

			if( !msRobots.ApplyRobotRule( sURL ) ) {
				debug_msg( string.Format( "Disallowed by robots.txt: {0}", sURL ), 1 );
				return( false );
			}

			if( this.DocCollection.ContainsKey( sURL ) ) {

				debug_msg( string.Format( "ADDING INLINK FOR: {0}", sURL ), 2 );
				debug_msg( string.Format( "PARENT: {0}", sParentURL ), 3 );
								
				msDoc = ( MacroscopeDocument )this.DocCollection[sURL];
				if( msDoc != null ) {
					msDoc.AddHyperlinkIn( sParentURL );
				}
				return( true );

			} else {
				this.DocCollection.Add( sURL, msDoc );
				msDoc.AddHyperlinkIn( sParentURL );
			}
			
			if( msDoc.Depth > this.Depth ) {
				//debug_msg( string.Format( "TOO DEEP: {0}", msDoc.depth ), 3 );
				this.DocCollection.Remove( sURL );
				return( true );
			}

			if( this.ProbeHrefLangs ) {
				msDoc.probe_hreflangs = true;
			}

			if( msDoc.Execute() ) {
			
				this.PageLimitCount++;

				{
					string sLocale = msDoc.Locale;
					Hashtable htHrefLangs = ( Hashtable )msDoc.GetHrefLangs();
					if( sLocale != null ) {
						if( !this.Locales.ContainsKey( sLocale ) ) {
							this.Locales[sLocale] = sLocale;
						}
					}
					foreach( string sKeyLocale in htHrefLangs.Keys ) {
						if( !this.Locales.ContainsKey( sKeyLocale ) ) {
							this.Locales[sKeyLocale] = sKeyLocale;
						}
					}
				}

				Hashtable htOutlinks = msDoc.GetOutlinks();

				foreach( string sOutlinkKey in htOutlinks.Keys ) {
					string sOutlinkURL = ( string )htOutlinks[sOutlinkKey];
					//debug_msg( string.Format( "Outlink: {0}", sOutlinkURL ), 2 );

					if( sOutlinkURL != null ) {

						Boolean bProceed = true;

						if( this.PageLimit < 0 ) {
							bProceed = true;
						} else if( this.PageLimit > -1 ) {
							if( this.PageLimitCount >= this.PageLimit ) {
								debug_msg( string.Format( "PAGE LIMIT REACHED: {0} :: {1}", this.PageLimit, this.PageLimitCount ), 2 );
								bProceed = false;
							}
						}

						if( bProceed ) {
							if( MacroscopeURLTools.verify_same_host( this.StartUrl, sOutlinkURL ) ) {

								if( this.History.ContainsKey( sOutlinkURL ) ) {
									//debug_msg( string.Format( "ALREADY SEEN: {0}", sOutlinkURL ), 2 );
								} else {
									debug_msg( string.Format( "RECURSING INTO: {0}", sOutlinkURL ), 2 );
									this.PagesFound++;
									this.History.Add( sOutlinkURL, true );

									this.msJobThread.Update();

									this.Recurse( sURL, sOutlinkURL, iDepth + 1 );
								}

							} else {
								//debug_msg( string.Format( "FOREIGN HOST: {0}", sOutlinkURL ), 2 );
							}

						} else {
							break;
						}

					}

				}

			} else {
				debug_msg( string.Format( "EXECUTE FAILED: {0}", sURL ), 2 );
			}

			return( true );
		}
	
		 */

		/**************************************************************************/

	}

}
