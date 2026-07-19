import { Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { OrdersStore } from '../../orders.store';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './order-detail.component.html',
  styleUrl: './order-detail.component.scss',
})
export class OrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  protected readonly ordersStore = inject(OrdersStore);

  async ngOnInit(): Promise<void> {
    const orderId = this.route.snapshot.paramMap.get('orderId');
    if (orderId) {
      await this.ordersStore.loadOrderDetail(orderId);
    }
  }

  protected formatAmount(cents: number): string {
    return (cents / 100).toFixed(2) + ' €';
  }
}
