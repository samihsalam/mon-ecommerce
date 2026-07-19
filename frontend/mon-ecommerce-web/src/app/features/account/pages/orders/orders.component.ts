import { Component, inject, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';

import { OrdersStore } from '../../orders.store';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [RouterLink, DatePipe],
  templateUrl: './orders.component.html',
  styleUrl: './orders.component.scss',
})
export class OrdersComponent implements OnInit {
  protected readonly ordersStore = inject(OrdersStore);

  async ngOnInit(): Promise<void> {
    await this.ordersStore.loadOrders();
  }

  protected formatAmount(cents: number): string {
    return (cents / 100).toFixed(2) + ' €';
  }
}
