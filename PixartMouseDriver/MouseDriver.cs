using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace PixartMouseDriver
{
    class MouseDriver
    {
        private int largestPacketNumber;
        private bool mouseUp;
        private int previousSize;

        private double scaleFactor;

        public MouseDriver()
        {
            largestPacketNumber = -1;
            mouseUp = true;
            previousSize = -1;

            scaleFactor = 4;
        }

        public void handlePacket(int[] coordinates)
        {
            // Filter negative coordinates out
            coordinates = coordinates.Where(s => s >= 0).ToArray();

            // Reject packets without any present LED's 
            if (coordinates.Length < 4)
            {
                return;
            }

            // Reject out of order packets
            if (coordinates[0] < largestPacketNumber)
            {
                return;
            }
            largestPacketNumber = coordinates[0];

            const int blobLength = 3;
            const int blobStart = 1;
            const int blobSizeOffset = 2;

            // Select largest blob
            int t = blobStart + blobSizeOffset;
            for (int i = t + blobLength; i < coordinates.Length; i += blobLength)
            {
                if (coordinates[t] < coordinates[i])
                {
                    t = i;
                }
            }
            t -= blobSizeOffset;

            // Use index of first non-empty blob
            double x = coordinates[t];
            double y = coordinates[t+1];

            // Scale motions, and center on screen center
            const int cameraWidth = 1024;
            const int cameraHeight = 768;
            int screenWidth = Screen.PrimaryScreen.Bounds.Width;
            int screenHeight = Screen.PrimaryScreen.Bounds.Height;
            x -= cameraWidth / 2;
            y -= cameraHeight / 2;
            x *= scaleFactor;
            y *= scaleFactor;
            x += screenHeight / 2;
            y += screenWidth / 2;

            // Move mouse
            MouseOperations.SetCursorPosition((int)Math.Floor(x), (int)Math.Floor(y));

            // Use mouse size threshold to send click events
            int size = coordinates[t + 2];
            if (size >= 4)
            {
                if (mouseUp)
                {
                    mouseUp = false;
                    MouseOperations.MouseEvent(MouseOperations.MouseEventFlags.LeftDown);
                }
            }
            else
            {
                if (!mouseUp)
                {
                    mouseUp = true;
                    MouseOperations.MouseEvent(MouseOperations.MouseEventFlags.LeftUp);
                }
            }

            previousSize = size;
        }
    }
}
