using UnityEngine;

namespace FishBreeder.Store
{
    /// <summary>The board on the ticket booth's roof: shows today's ticket price, kept up to date.</summary>
    public class TicketBoothSign : MonoBehaviour
    {
        TextMesh text;
        int shown = -1;

        void Awake() => text = GetComponent<TextMesh>();

        void Update()
        {
            if (text == null || shown == PublicAquarium.TicketPrice) return;
            shown = PublicAquarium.TicketPrice;
            text.text = $"TICKETS\n<size=40>${shown}</size>";
        }
    }
}
