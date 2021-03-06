//
// System.Data.Odbc.OdbcCommand
//
// Authors:
//   Brian Ritchie (brianlritchie@hotmail.com)
//
// Copyright (C) Brian Ritchie, 2002
//

//
// Copyright (C) 2004 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Collections;
using System.Runtime.InteropServices;

namespace System.Data.Odbc
{
	/// <summary>
	/// Represents an SQL statement or stored procedure to execute against a data source.
	/// </summary>
	[DesignerAttribute ("Microsoft.VSDesigner.Data.VS.OdbcCommandDesigner, "+ Consts.AssemblyMicrosoft_VSDesigner, "System.ComponentModel.Design.IDesigner")]
	[ToolboxItemAttribute ("System.Drawing.Design.ToolboxItem, "+ Consts.AssemblySystem_Drawing)]
	[DefaultEvent ("RecordsAffected")]
	public sealed class OdbcCommand : DbCommand, ICloneable
	{
		#region Fields

		const int DEFAULT_COMMAND_TIMEOUT = 30;

		string commandText;
		int timeout;
		CommandType commandType;
		UpdateRowSource updateRowSource;

		OdbcConnection connection;
		OdbcTransaction transaction;
		OdbcParameterCollection _parameters;

		bool designTimeVisible;
		bool prepared;
		IntPtr hstmt = IntPtr.Zero;
		object generation = null; // validity of hstmt

		bool disposed;
		
		#endregion // Fields

		#region Constructors

		public OdbcCommand ()
		{
			timeout = DEFAULT_COMMAND_TIMEOUT;
			commandType = CommandType.Text;
			_parameters = new OdbcParameterCollection ();
			designTimeVisible = true;
			updateRowSource = UpdateRowSource.Both;
		}

		public OdbcCommand (string cmdText) : this ()
		{
			commandText = cmdText;
		}

		public OdbcCommand (string cmdText, OdbcConnection connection)
			: this (cmdText)
		{
			Connection = connection;
		}

		public OdbcCommand (string cmdText, OdbcConnection connection,
				    OdbcTransaction transaction) : this (cmdText, connection)
		{
			this.Transaction = transaction;
		}

		#endregion // Constructors

		#region Properties

		internal IntPtr hStmt {
			get { return hstmt; }
		}

		[OdbcCategory ("Data")]
		[DefaultValue ("")]
		[OdbcDescriptionAttribute ("Command text to execute")]
		[EditorAttribute ("Microsoft.VSDesigner.Data.Odbc.Design.OdbcCommandTextEditor, "+ Consts.AssemblyMicrosoft_VSDesigner, "System.Drawing.Design.UITypeEditor, "+ Consts.AssemblySystem_Drawing )]
		[RefreshPropertiesAttribute (RefreshProperties.All)]
		public
		override
		string CommandText {
			get {
				if (commandText == null)
					return string.Empty;
				return commandText;
			}
			set {
				prepared = false;
				commandText = value;
			}
		}

		[OdbcDescriptionAttribute ("Time to wait for command to execute")]
		public override
		int CommandTimeout {
			get { return timeout; }
			set {
				if (value < 0)
					throw new ArgumentException ("The property value assigned is less than 0.",
						"CommandTimeout");
				timeout = value;
			}
		}

		[OdbcCategory ("Data")]
		[DefaultValue ("Text")]
		[OdbcDescriptionAttribute ("How to interpret the CommandText")]
		[RefreshPropertiesAttribute (RefreshProperties.All)]
		public
		override
		CommandType CommandType {
			get { return commandType; }
			set {
				ExceptionHelper.CheckEnumValue (typeof (CommandType), value);
				commandType = value;
			}
		}


		[DefaultValue (null)]
		[EditorAttribute ("Microsoft.VSDesigner.Data.Design.DbConnectionEditor, "+ Consts.AssemblyMicrosoft_VSDesigner, "System.Drawing.Design.UITypeEditor, "+ Consts.AssemblySystem_Drawing )]
		public new OdbcConnection Connection {
			get { return DbConnection as OdbcConnection; }
			set { DbConnection = value; }
		}

		[BrowsableAttribute (false)]
		[DesignOnlyAttribute (true)]
		[DefaultValue (true)]
		[EditorBrowsable (EditorBrowsableState.Never)]
		public
		override
		bool DesignTimeVisible {
			get { return designTimeVisible; }
			set { designTimeVisible = value; }
		}

		[OdbcCategory ("Data")]
		[OdbcDescriptionAttribute ("The parameters collection")]
		[DesignerSerializationVisibilityAttribute (DesignerSerializationVisibility.Content)]
		public
		new
		OdbcParameterCollection Parameters {
			get {
				return base.Parameters as OdbcParameterCollection;
			}
		}
		
		[BrowsableAttribute (false)]
		[OdbcDescriptionAttribute ("The transaction used by the command")]
		[DesignerSerializationVisibilityAttribute (DesignerSerializationVisibility.Hidden)]
		public
		new
		OdbcTransaction Transaction {
			get { return transaction; }
			set { transaction = value; }
		}

		[OdbcCategory ("Behavior")]
		[DefaultValue (UpdateRowSource.Both)]
		[OdbcDescriptionAttribute ("When used by a DataAdapter.Update, how command results are applied to the current DataRow")]
		public
		override
		UpdateRowSource UpdatedRowSource {
			get { return updateRowSource; }
			set {
				ExceptionHelper.CheckEnumValue (typeof (UpdateRowSource), value);
				updateRowSource = value;
			}
		}

		protected override DbConnection DbConnection {
			get { return connection; }
			set { connection = (OdbcConnection) value;}
		}


		protected override DbParameterCollection DbParameterCollection {
			get { return _parameters as DbParameterCollection;}
		}

		protected override DbTransaction DbTransaction {
			get { return transaction; }
			set { transaction = (OdbcTransaction) value; }
		}

		#endregion // Properties

		#region Methods

		public
		override
		void Cancel ()
		{
			if (hstmt != IntPtr.Zero) {
				OdbcReturn Ret = libodbc.SQLCancel (hstmt);
				if (Ret != OdbcReturn.Success && Ret != OdbcReturn.SuccessWithInfo)
					throw connection.CreateOdbcException (OdbcHandleType.Stmt, hstmt);
			} else
				throw new InvalidOperationException ();
		}

		protected override DbParameter CreateDbParameter ()
		{
			return CreateParameter ();
		}

		public new OdbcParameter CreateParameter ()
		{
			return new OdbcParameter ();
		}

		internal void Unlink ()
		{
			if (disposed)
				return;

			FreeStatement (false);
		}

		protected override void Dispose (bool disposing)
		{
			if (disposed)
				return;

			FreeStatement (); // free handles
			CommandText = null;
			Connection = null;
			Transaction = null;
			Parameters.Clear ();
			disposed = true;
		}

		private IntPtr ReAllocStatment ()
		{
			OdbcReturn ret;

			if (hstmt != IntPtr.Zero)
				// Free the existing hstmt.  Also unlinks from the connection.
				FreeStatement ();
			// Link this command to the connection.  The hstmt created below
			// only remains valid while generation == Connection.generation.
			generation = Connection.Link (this);
			ret = libodbc.SQLAllocHandle (OdbcHandleType.Stmt, Connection.hDbc, ref hstmt);
			if (ret != OdbcReturn.Success && ret != OdbcReturn.SuccessWithInfo)
				throw connection.CreateOdbcException (OdbcHandleType.Dbc, Connection.hDbc);
			disposed = false;
			return hstmt;
		}

		void FreeStatement ()
		{
			FreeStatement (true);
		}

		private void FreeStatement (bool unlink)
		{
			prepared = false;

			if (hstmt == IntPtr.Zero)
				return;

			// Normally the command is unlinked from the connection, but during
			// OdbcConnection.Close() this would be pointless and (quadratically)
			// slow.
			if (unlink)
				Connection.Unlink (this);

			// Serialize with respect to the connection's own destruction
			lock(Connection) {
				// If the connection has already called SQLDisconnect then hstmt
				// may have already been freed, in which case it is not safe to
				// use.  Thus the generation check.
				if(Connection.Generation == generation) {
					// free previously allocated handle.
					OdbcReturn ret = libodbc.SQLFreeStmt (hstmt, libodbc.SQLFreeStmtOptions.Close);
					if ((ret!=OdbcReturn.Success) && (ret!=OdbcReturn.SuccessWithInfo))
						throw connection.CreateOdbcException (OdbcHandleType.Stmt, hstmt);
			
					ret = libodbc.SQLFreeHandle ((ushort) OdbcHandleType.Stmt, hstmt);
					if (ret != OdbcReturn.Success && ret != OdbcReturn.SuccessWithInfo)
						throw connection.CreateOdbcException (OdbcHandleType.Stmt, hstmt);
				}
				hstmt = IntPtr.Zero;
			}
		}
		
		private void ExecSQL (CommandBehavior behavior, bool createReader, string sql)
		{
			OdbcReturn ret;

			if (!prepared && Parameters.Count == 0) {
				ReAllocStatment ();

				ret = libodbc.SQLExecDirect (hstmt, sql, libodbc.SQL_NTS);
				if (ret != OdbcReturn.Success && ret != OdbcReturn.SuccessWithInfo && ret != OdbcReturn.NoData)
					throw connection.CreateOdbcException (OdbcHandleType.Stmt, hstmt);
				return;
			}

			if (!prepared)
				Prepare();

			BindParameters ();
			ret = libodbc.SQLExecute (hstmt);
			if (ret != OdbcReturn.Success && ret != OdbcReturn.SuccessWithInfo && ret != OdbcReturn.NoData)
				throw connection.CreateOdbcException (OdbcHandleType.Stmt, hstmt);
		}

		internal void FreeIfNotPrepared ()
		{
			if (! prepared)
				FreeStatement ();
		}

		public
		override
		int ExecuteNonQuery ()
		{
			return ExecuteNonQuery ("ExecuteNonQuery", CommandBehavior.Default, false);
		}

		private int ExecuteNonQuery (string method, CommandBehavior behavior, bool createReader)
		{
			int records = 0;
			if (Connection == null)
				throw new InvalidOperationException (string.Format (
					"{0}: Connection is not set.", method));
			if (Connection.State == ConnectionState.Closed)
				throw new InvalidOperationException (string.Format (
					"{0}: Connection state is closed", method));
			if (CommandText.Length == 0)
				throw new InvalidOperationException (string.Format (
					"{0}: CommandText is not set.", method));

			ExecSQL (behavior, createReader, CommandText);

			// .NET documentation says that except for INSERT, UPDATE and
			// DELETE  where the return value is the number of rows affected
			// for the rest of the commands the return value is -1.
			if ((CommandText.ToUpper().IndexOf("UPDATE")!=-1) ||
			    (CommandText.ToUpper().IndexOf("INSERT")!=-1) ||
			    (CommandText.ToUpper().IndexOf("DELETE")!=-1)) {
				int numrows = 0;
				libodbc.SQLRowCount (hstmt, ref numrows);
				records = numrows;
			} else
				records = -1;

			if (!createReader && !prepared)
				FreeStatement ();
			
			return records;
		}

		public
		override
		void Prepare()
		{
			ReAllocStatment ();
			
			OdbcReturn ret;
			ret = libodbc.SQLPrepare(hstmt, CommandText, CommandText.Length);
			if ((ret!=OdbcReturn.Success) && (ret!=OdbcReturn.SuccessWithInfo))
				throw connection.CreateOdbcException (OdbcHandleType.Stmt, hstmt);
			prepared = true;
		}

		private void BindParameters ()
		{
			int i = 1;
			foreach (OdbcParameter p in Parameters) {
				p.Bind (this, hstmt, i);
				p.CopyValue ();
				i++;
			}
		}

		public
		new
		OdbcDataReader ExecuteReader ()
		{
			return ExecuteReader (CommandBehavior.Default);
		}

		protected override DbDataReader ExecuteDbDataReader (CommandBehavior behavior)
		{
			return ExecuteReader (behavior);
		}

		public
		new
		OdbcDataReader ExecuteReader (CommandBehavior behavior)
		{
			return ExecuteReader ("ExecuteReader", behavior);
		}

		OdbcDataReader ExecuteReader (string method, CommandBehavior behavior)
		{
			int recordsAffected = ExecuteNonQuery (method, behavior, true);
			OdbcDataReader dataReader = new OdbcDataReader (this, behavior, recordsAffected);
			return dataReader;
		}


		public
		override
		object ExecuteScalar ()
		{
			object val = null;
			OdbcDataReader reader = ExecuteReader ("ExecuteScalar",
				CommandBehavior.Default);
			try {
				if (reader.Read ())
					val = reader [0];
			} finally {
				reader.Close ();
			}
			return val;
		}

		object ICloneable.Clone ()
		{
			OdbcCommand command = new OdbcCommand ();
			command.CommandText = this.CommandText;
			command.CommandTimeout = this.CommandTimeout;
			command.CommandType = this.CommandType;
			command.Connection = this.Connection;
			command.DesignTimeVisible = this.DesignTimeVisible;
			foreach (OdbcParameter parameter in this.Parameters)
				command.Parameters.Add (parameter);
			command.Transaction = this.Transaction;
			return command;
		}

		public void ResetCommandTimeout ()
		{
			CommandTimeout = DEFAULT_COMMAND_TIMEOUT;
		}

		#endregion
	}
}
