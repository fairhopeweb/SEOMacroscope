﻿/*
	
	This file is part of SEOMacroscope.
	
	Copyright 2017 Jason Holland.
	
	The GitHub repository may be found at:
	
		https://github.com/nazuke/SEOMacroscope
	
	Foobar is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.
	
	Foobar is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.
	
	You should have received a copy of the GNU General Public License
	along with Foobar.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.Text.RegularExpressions;
using System.Net;

namespace SEOMacroscope
{

	public partial class MacroscopeDocument : Macroscope
	{

		/**************************************************************************/
		
		Boolean IsImagePage ()
		{
			
			HttpWebRequest req = null;
			HttpWebResponse res = null;
			Boolean bIs = false;
			Regex reIs = new Regex ( "^image/(gif|png|jpeg|bmp|webp)", RegexOptions.IgnoreCase );
			string sErrorCondition = null;
						
			try {

				req = WebRequest.CreateHttp( this.Url );
				req.Method = "HEAD";
				req.Timeout = this.Timeout;
				req.KeepAlive = false;
				MacroscopePreferencesManager.EnableHttpProxy( req );

				try {
					res = ( HttpWebResponse )req.GetResponse();
				} catch( WebException ex ) {
					DebugMsg( string.Format( "IsImagePage :: WebException: {0}", ex.Message ) );
					DebugMsg( string.Format( "IsImagePage :: WebExceptionStatus: {0}", ex.Status ) );
					sErrorCondition = ex.Status.ToString();
				}

				if( res != null ) {

					this.ProcessHttpHeaders( req, res );

					DebugMsg( string.Format( "Status: {0}", res.StatusCode ) );
					DebugMsg( string.Format( "ContentType: {0}", res.ContentType.ToString() ) );

					if( reIs.IsMatch( res.ContentType.ToString() ) ) {
						bIs = true;
					}

					res.Close();

				}

//			} catch( UriFormatException ex ) {
//				DebugMsg( string.Format( "IsImagePage :: UriFormatException: {0}", ex.Message ) );

			} catch( WebException ex ) {
				DebugMsg( string.Format( "IsImagePage :: WebException: {0}", ex.Message ) );
				DebugMsg( string.Format( "IsImagePage :: WebExceptionStatus: {0}", ex.Status ) );
				sErrorCondition = ex.Status.ToString();
			}
			
			if( sErrorCondition != null ) {
				this.StatusCode = 500;
				this.ErrorCondition = sErrorCondition;
			}
			
			return( bIs );
		}

		/**************************************************************************/
		
		void ProcessImagePage ()
		{

			HttpWebRequest req = null;
			HttpWebResponse res = null;
			string sErrorCondition = null;

			try {
				req = WebRequest.CreateHttp( this.Url );
				req.Method = "HEAD";
				req.Timeout = this.Timeout;
				req.KeepAlive = false;
				MacroscopePreferencesManager.EnableHttpProxy( req );

				try {
					res = ( HttpWebResponse )req.GetResponse();
				} catch( WebException ex ) {
					DebugMsg( string.Format( "ProcessImagePage :: WebException: {0}", ex.Message ) );
					DebugMsg( string.Format( "ProcessImagePage :: WebExceptionStatus: {0}", ex.Status ) );
					sErrorCondition = ex.Status.ToString();
				}

			} catch( WebException ex ) {
				DebugMsg( string.Format( "ProcessImagePage :: WebException: {0}", ex.Message ) );
				DebugMsg( string.Format( "ProcessImagePage :: WebExceptionStatus: {0}", ex.Status ) );
				sErrorCondition = ex.Status.ToString();
			}

			if( res != null ) {
								
				this.ProcessHttpHeaders( req, res );

				{ // Title

					MatchCollection reMatches = Regex.Matches( this.Url, "/([^/]+)$" );
					string sTitle = null;

					foreach( Match match in reMatches ) {
						if( match.Groups[ 1 ].Value.Length > 0 ) {
							sTitle = match.Groups[ 1 ].Value.ToString();
							break;
						}
					}

					if( sTitle != null ) {
						this.Title = sTitle;
						DebugMsg( string.Format( "TITLE: {0}", this.Title ) );
					} else {
						DebugMsg( string.Format( "TITLE: {0}", "MISSING" ) );
					}

				}

				res.Close();

			} else {
				this.StatusCode = 500;
			}

			if( sErrorCondition != null ) {
				this.StatusCode = 500;
				this.ErrorCondition = sErrorCondition;
			}
			
		}

		/**************************************************************************/

	}

}
