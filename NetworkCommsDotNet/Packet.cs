﻿//  Copyright 2011-2012 Marc Fletcher, Matthew Dean
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
//  A commercial license of this software can also be purchased. 
//  Please see <http://www.networkcommsdotnet.com/licenses> for details.

using System;
using System.Collections.Generic;
using System.Text;
using DPSBase;
using System.IO;

namespace NetworkCommsDotNet
{
    /// <summary>
    /// Wrapper for <see cref="PacketHeader"/> and packetData.
    /// </summary>
    public class Packet
    {
        PacketHeader packetHeader;
        StreamSendWrapper packetData;

        /// <summary>
        /// Create a new packet
        /// </summary>
        /// <param name="packetTypeStr">The sending packet type</param>
        /// <param name="packetObject">The object to be sent</param>
        /// <param name="options">The <see cref="SendReceiveOptions"/> to be used to create this packet</param>
        public Packet(string packetTypeStr, object packetObject, SendReceiveOptions options)
        {
            Constructor(packetTypeStr, null, packetObject, options);
        }

        /// <summary>
        /// Create a new packet
        /// </summary>
        /// <param name="sendingPacketTypeStr">The sending packet type</param>
        /// <param name="requestReturnPacketTypeStr">The expected return packet type</param>
        /// <param name="packetObject">The object to be sent</param>
        /// <param name="options">The <see cref="SendReceiveOptions"/> to be used to create this packet</param>
        public Packet(string sendingPacketTypeStr, string requestReturnPacketTypeStr, object packetObject, SendReceiveOptions options)
        {
            Constructor(sendingPacketTypeStr, requestReturnPacketTypeStr, packetObject, options);
        }

        private void Constructor(string sendingPacketTypeStr, string requestReturnPacketTypeStr, object packetObject, SendReceiveOptions options)
        {
            if (sendingPacketTypeStr == null || sendingPacketTypeStr == "") throw new ArgumentNullException("The provided packetTypeStr can not be zero length or null.");
            if (options == null) throw new ArgumentNullException("The provided SendReceiveOptions cannot be null.");

            if (packetObject == null)
                this.packetData = new StreamSendWrapper(new ThreadSafeStream(new MemoryStream(new byte[0], 0, 0, false, true), true));
            else
            {
                if (options.DataSerializer == null) throw new ArgumentNullException("The provided SendReceiveOptions should not contain a null DataSerializer.");
                this.packetData = options.DataSerializer.SerialiseDataObject(packetObject, options.DataProcessors, options.Options);
            }

            //We only calculate the checkSum if we are going to use it
            string hashStr = null;
            if (NetworkComms.EnablePacketCheckSumValidation)
                hashStr = NetworkComms.MD5Bytes(packetData.ThreadSafeStream.ToArray(packetData.Start, packetData.Length));

            this.packetHeader = new PacketHeader(sendingPacketTypeStr, packetData.Length, requestReturnPacketTypeStr,  
                options.Options.ContainsKey("ReceiveConfirmationRequired"),
                hashStr,
                options.Options.ContainsKey("IncludePacketConstructionTime"));

            //Add an identifier specifying the serializers and processors we have used
            this.packetHeader.SetOption(PacketHeaderLongItems.SerializerProcessors, DPSManager.CreateSerializerDataProcessorIdentifier(options.DataSerializer, options.DataProcessors));

            if (NetworkComms.LoggingEnabled) NetworkComms.Logger.Trace(" ... creating comms packet. PacketObject data size is " + packetData.Length + " bytes");
        }

        /// <summary>
        /// Return the packet header for this packet
        /// </summary>
        public PacketHeader PacketHeader
        {
            get { return packetHeader; }
        }

        /// <summary>
        /// Return the byte[] packet data
        /// </summary>
        public StreamSendWrapper PacketData
        {
            get { return packetData; }
        }

        /// <summary>
        /// Returns the serialisedbytes of the packet header appended by the serialised header size. This is required to rebuild the header on receive.
        /// </summary>
        /// <returns>The serialised header as byte[]</returns>
        public byte[] SerialiseHeader(SendReceiveOptions options)
        {
            //We need to start of by serialising the header
            byte[] serialisedHeader = options.DataSerializer.SerialiseDataObject(packetHeader, options.DataProcessors, null).ThreadSafeStream.ToArray(1);

            if (serialisedHeader.Length - 1 > byte.MaxValue)
                throw new SerialisationException("Unable to send packet as header size is larger than Byte.MaxValue. Try reducing the length of provided packetTypeStr or turning off checkSum validation.");

            //The first byte now specifies the header size (allows for variable header size)
            serialisedHeader[0] = (byte)(serialisedHeader.Length - 1);

            if (serialisedHeader == null)
                throw new SerialisationException("Serialised header bytes should never be null.");

            return serialisedHeader;
        }
    }
}
